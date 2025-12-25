{
  description = "CrossMacro - Cross-platform Mouse and Keyboard Macro Recorder and Player";

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

        crossmacroVersion = "0.7.1";

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
          xorg.libXfixes

          # GLib for GIO
          glib

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

        # The daemon package (Native AOT)
        crossmacro-daemon = pkgs.buildDotnetModule rec {
          pname = "crossmacro-daemon";
          version = crossmacroVersion;

          src = ./.;

          projectFile = "src/CrossMacro.Daemon/CrossMacro.Daemon.csproj";

          # Daemon dependencies are a subset of UI deps, so we can share deps.json
          nugetDeps = ./deps.json;

          dotnet-sdk = pkgs.dotnet-sdk_10;
          # Native AOT is self-contained, no runtime needed
          dotnet-runtime = null;

          executables = [ "CrossMacro.Daemon" ];

          buildType = "Release";

          # Enable self-contained build for Native AOT
          selfContainedBuild = true;

          # Native AOT requires clang and zlib for compilation
          nativeBuildInputs = with pkgs; [
            clang
            zlib
          ];

          dotnetFlags = [
            "-p:Version=${version}"
          ];

          # Install polkit policy file
          postInstall = ''
            install -Dm644 scripts/assets/org.crossmacro.policy $out/share/polkit-1/actions/org.crossmacro.policy
          '';

          meta = with pkgs.lib; {
            description = "Privileged Daemon for CrossMacro";
            platforms = platforms.linux;
            mainProgram = "CrossMacro.Daemon";
            maintainers = with maintainers; [ ];
          };
        };

        # The main CrossMacro package
        crossmacro = pkgs.buildDotnetModule rec {
          pname = "crossmacro";
          version = crossmacroVersion;

          src = ./.;

          projectFile = "src/CrossMacro.UI/CrossMacro.UI.csproj";

          # NuGet dependencies lock file
          nugetDeps = ./deps.json;

          # .NET 10 Preview support
          dotnet-sdk = pkgs.dotnet-sdk_10;
          dotnet-runtime = pkgs.dotnet-runtime_10;

          executables = [ "CrossMacro.UI" ];

          buildType = "Release";

          # Disable self-contained to use system runtime
          dotnetFlags = [
            "-p:SelfContained=false"
            "-p:Version=${version}"
          ];

          # Runtime dependencies for Avalonia/SkiaSharp
          runtimeDeps = runtimeLibs;

          postInstall = ''
            install -Dm644 scripts/assets/CrossMacro.desktop $out/share/applications/crossmacro.desktop
            
            for size in 16 32 48 64 128 256 512; do
              mkdir -p $out/share/icons/hicolor/''${size}x''${size}/apps
              install -Dm644 src/CrossMacro.UI/Assets/icons/''${size}x''${size}/apps/crossmacro.png $out/share/icons/hicolor/''${size}x''${size}/apps/crossmacro.png
            done
            install -Dm644 scripts/assets/com.github.alper-han.CrossMacro.appdata.xml $out/share/metainfo/com.github.alper-han.CrossMacro.appdata.xml
          '';

          meta = with pkgs.lib; {
            description = "Cross-platform mouse and keyboard macro recorder and player supporting Wayland/X11";
            homepage = "https://github.com/alper-han/CrossMacro";
            license = licenses.gpl3Plus;
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
          
          # Daemon
          daemon = crossmacro-daemon;

          # Explicit FHS package
          crossmacro-fhs = crossmacro-fhs;
        };

        # 'nix run' will use this
        apps = {
          default = {
            type = "app";
            program = pkgs.lib.getExe crossmacro-fhs;
            meta = crossmacro-fhs.meta;
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
            echo "CrossMacro Development Environment"
            echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
            echo "Dotnet SDK: $(dotnet --version)"
            echo "Systemd service required for Input Access."
            echo ""
            echo "Commands:"
            echo "  dotnet run --project src/CrossMacro.UI/CrossMacro.UI.csproj"
            echo "  dotnet build"
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
          daemonPkg = self.packages.${pkgs.stdenv.hostPlatform.system}.daemon;
        in
        {
          options.programs.crossmacro = {
            enable = mkEnableOption "CrossMacro mouse macro recorder";

            package = mkOption {
              type = types.package;
              default = self.packages.${pkgs.stdenv.hostPlatform.system}.default;
              description = "The CrossMacro UI package to use";
            };
            
            daemonPackage = mkOption {
              type = types.package;
              default = daemonPkg;
              description = "The CrossMacro Daemon package to use";
            };

            users = mkOption {
              type = types.listOf types.str;
              default = [ ];
              description = "Users to add to the crossmacro group for daemon communication";
            };
          };

          config = mkIf cfg.enable {
            assertions = [
              {
                assertion = cfg.users != [ ];
                message = "CrossMacro: You must configure at least one user to access the input daemon.\n       Please set `programs.crossmacro.users = [ \"yourusername\" ];` in your NixOS configuration.";
              }
            ];

            environment.systemPackages = [ cfg.package ];

            # Enable uinput for virtual input device creation (required for playback)
            hardware.uinput.enable = true;

            # Fix uinput permissions - NixOS default uses ACLs but group perms are ---
            # This ensures the input group has read/write access
            services.udev.extraRules = ''
              KERNEL=="uinput", GROUP="input", MODE="0660", OPTIONS+="static_node=uinput"
            '';

            # Install polkit policy for authorization dialogs
            environment.etc."polkit-1/actions/org.crossmacro.policy".source = 
              "${cfg.daemonPackage}/share/polkit-1/actions/org.crossmacro.policy";

            users.groups.crossmacro = {};

            # Add specified users to the crossmacro group and define the daemon user
            users.users = builtins.listToAttrs (map (user: {
              name = user;
              value = { extraGroups = [ "crossmacro" ]; };
            }) cfg.users) // {
              crossmacro = {
                 isSystemUser = true;
                 group = "input";
                 extraGroups = [ "crossmacro" "uinput" ];
                 description = "CrossMacro Input Daemon User";
              };
            };

            systemd.services.crossmacro = {
              description = "CrossMacro Input Daemon Service";
              wantedBy = [ "multi-user.target" ];
              after = [ "network.target" "dbus.service" "polkit.service" ];
              wants = [ "dbus.service" "polkit.service" ];
              path = [ pkgs.polkit ]; # For pkcheck command
              serviceConfig = {
                Type = "simple";
                User = "crossmacro";
                Group = "input";
                ExecStart = "${lib.getExe cfg.daemonPackage}";
                Restart = "always";
                RestartSec = 5;
                RuntimeDirectory = "crossmacro";
                RuntimeDirectoryMode = "0755";
              };
            };
          };
        };
    };
}
