{
  description = "CrossMacro - Cross-platform Mouse Macro Recorder and Player";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
    flake-utils.url = "github:numtide/flake-utils";
  };

  outputs =
    {
      self,
      nixpkgs,
      flake-utils,
    }:
    flake-utils.lib.eachDefaultSystem (
      system:
      let
        pkgs = nixpkgs.legacyPackages.${system};

        # Runtime libraries required by Avalonia/SkiaSharp on Linux
        # These are critical for FHS environment to work correctly
        runtimeLibs = with pkgs; [
          # Core GUI dependencies
          fontconfig
          freetype
          expat

          # X11 dependencies (Required by Avalonia/SkiaSharp)
          xorg.libX11
          xorg.libICE
          xorg.libSM
          xorg.libXi
          xorg.libXcursor
          xorg.libXext
          xorg.libXrandr
          xorg.libXrender
          xorg.libXinerama
          xorg.libXfixes

          # GTK/GNOME dependencies
          glib
          gtk3

          # Graphics/OpenGL
          libglvnd
          mesa

          # Core system libraries
          zlib
          icu
          openssl

          # Wayland support
          wayland
          libxkbcommon

          # Additional dependencies
          krb5
          stdenv.cc.cc.lib # libstdc++
        ];

        # The main CrossMacro package
        crossmacro = pkgs.buildDotnetModule rec {
          pname = "crossmacro";
          version = "0.4.1";

          src = ./.;

          projectFile = "src/CrossMacro.UI/CrossMacro.UI.csproj";

          # NuGet dependencies lock file
          nugetDeps = ./deps.nix;

          # .NET 10 Preview support
          dotnet-sdk = pkgs.dotnet-sdk_10;
          dotnet-runtime = pkgs.dotnet-runtime_10;

          executables = [ "CrossMacro.UI" ];

          buildType = "Release";

          # Disable self-contained to use system runtime
          dotnetFlags = [
            "-p:PublishSingleFile=false"
            "-p:SelfContained=false"
            "-p:Version=${version}"
          ];

          # Ensure libraries are found during build/test if needed
          makeWrapperArgs = [
            "--prefix LD_LIBRARY_PATH : ${pkgs.lib.makeLibraryPath runtimeLibs}"
          ];

          postInstall = ''
            install -Dm644 scripts/assets/CrossMacro.desktop $out/share/applications/crossmacro.desktop
            install -Dm644 src/CrossMacro.UI/Assets/mouse-icon.png $out/share/icons/hicolor/512x512/apps/crossmacro.png
            install -Dm644 scripts/assets/com.github.alper-han.CrossMacro.appdata.xml $out/share/metainfo/com.github.alper-han.CrossMacro.appdata.xml
          '';

          meta = with pkgs.lib; {
            description = "Mouse macro recorder and player supporting Hyprland, KDE Plasma, and GNOME Shell";
            homepage = "https://github.com/alper-han/CrossMacro";
            license = licenses.gpl3;
            platforms = platforms.linux;
            mainProgram = "CrossMacro.UI";
            maintainers = with maintainers; [ ];
          };
        };

        # FHS environment wrapper for maximum compatibility
        # This ensures SkiaSharp/Avalonia can find all native libraries (libX11, fontconfig, etc.)
        crossmacro-fhs = pkgs.buildFHSEnv {
          name = "crossmacro";

          targetPkgs =
            tpkgs:
            [
              crossmacro
              tpkgs.dotnet-runtime_10
            ]
            ++ runtimeLibs;

          runScript = "CrossMacro.UI";

          extraInstallCommands = ''
            mkdir -p $out/share
            ln -s ${crossmacro}/share/applications $out/share/applications
            ln -s ${crossmacro}/share/icons $out/share/icons
            ln -s ${crossmacro}/share/metainfo $out/share/metainfo
          '';

          meta = crossmacro.meta // {
            description = "CrossMacro wrapped in FHS environment (Recommended for Avalonia)";
            mainProgram = "crossmacro";
          };
        };

      in
      {
        packages = {
          # Default to FHS version for 'nix build'
          default = crossmacro-fhs;

          # Raw package
          crossmacro = crossmacro;

          # Explicit FHS package
          crossmacro-fhs = crossmacro-fhs;
        };

        # 'nix run' will use this
        apps = {
          default = {
            type = "app";
            program = pkgs.lib.getExe crossmacro-fhs;
          };
        };

        devShells.default = pkgs.mkShell {
          buildInputs =
            with pkgs;
            [
              dotnet-sdk_10
              git
            ]
            ++ runtimeLibs;

          LD_LIBRARY_PATH = "${pkgs.lib.makeLibraryPath runtimeLibs}";

          shellHook = ''
            echo "🚀 CrossMacro Development Environment"
            echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
            echo "Dotnet SDK: $(dotnet --version)"
            echo ""
            echo "Commands:"
            echo "  dotnet run --project src/CrossMacro.UI/CrossMacro.UI.csproj"
            echo "  dotnet build"
            echo ""
            echo "⚠️  Input Device Access Required:"
            echo "   sudo usermod -aG input \$USER"
            echo "   (logout and login again after)"
            echo ""
          '';
        };

        formatter = pkgs.nixfmt-rfc-style;

      }
    )
    // {
      # NixOS module for system-wide installation
      nixosModules.default =
        {
          config,
          lib,
          pkgs,
          ...
        }:
        with lib;
        let
          cfg = config.programs.crossmacro;
        in
        {
          options.programs.crossmacro = {
            enable = mkEnableOption "CrossMacro mouse macro recorder";

            package = mkOption {
              type = types.package;
              default = self.packages.${pkgs.system}.default;
              description = "The CrossMacro package to use";
            };

            addUsersToInputGroup = mkOption {
              type = types.bool;
              default = true;
              description = "Whether to add all normal users to the input group";
            };
          };

          config = mkIf cfg.enable {
            environment.systemPackages = [ cfg.package ];

            # Automatically add all normal users to input group
            users.groups.input.members = mkIf cfg.addUsersToInputGroup (
              attrNames (filterAttrs (_: user: user.isNormalUser) config.users.users)
            );

            # Add udev rules for input device access
            services.udev.extraRules = ''
              # Allow members of input group to access input devices
              KERNEL=="event*", SUBSYSTEM=="input", MODE="0660", GROUP="input"
              KERNEL=="uinput", SUBSYSTEM=="misc", MODE="0660", GROUP="input"
            '';
          };
        };
    };
}
