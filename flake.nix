{
  description = "CrossMacro - Cross-platform Mouse and Keyboard Macro Recorder and Player";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
    flake-parts.url = "github:hercules-ci/flake-parts";
  };

  outputs =
    inputs@{ flake-parts, ... }:
    flake-parts.lib.mkFlake { inherit inputs; } {
      systems = [
        "x86_64-linux"
        "aarch64-linux"
        "x86_64-darwin"
        "aarch64-darwin"
      ];

      perSystem =
        {
          config,
          self',
          inputs',
          pkgs,
          system,
          ...
        }:
        let
          versionFileContent = builtins.readFile ./VERSION;
          normalizedVersion = builtins.replaceStrings [ "\n" "\r" " " "\t" ] [ "" "" "" "" ] versionFileContent;
          versionMatch = builtins.match "([0-9]+\\.[0-9]+\\.[0-9]+)" normalizedVersion;
          crossmacroVersion =
            if versionMatch == null then
              throw "Invalid VERSION file format. Expected X.Y.Z"
            else
              builtins.elemAt versionMatch 0;

          # Core system libraries required by .NET on both Linux and macOS
          commonLibs = with pkgs; [
            zlib
            icu
            openssl
          ];

          # Context: https://github.com/AvaloniaUI/Avalonia/wiki/Linux-Dependencies
          linuxLibs = with pkgs; [
             # Core GUI dependencies
            fontconfig
            freetype
            expat

            # X11 dependencies (Required by Avalonia/SkiaSharp)
            libx11
            libice
            libsm
            libxi
            libxcursor
            libxext
            libxrandr
            libxrender
            libxfixes
            libxtst

            # GLib for GIO
            glib

            # Graphics/OpenGL
            libglvnd
            mesa

            # Wayland support
            wayland
            libxkbcommon
          ];

          # Runtime libraries
          runtimeLibs = commonLibs ++ (if pkgs.stdenv.isLinux then linuxLibs else [ ]);
          uiHostProject =
            if pkgs.stdenv.isDarwin then
              "src/CrossMacro.UI.MacOS/CrossMacro.UI.MacOS.csproj"
            else
              "src/CrossMacro.UI.Linux/CrossMacro.UI.Linux.csproj";

          commonDotnetModule = {
            pname = "crossmacro";
            version = crossmacroVersion;
            src = ./.;
            nugetDeps = ./deps.json;
            dotnet-sdk = pkgs.dotnet-sdk_10;
          };

          # The daemon package (Native AOT) - Linux Only
          crossmacro-daemon =
            if pkgs.stdenv.isLinux then
              pkgs.buildDotnetModule (
                commonDotnetModule
                // {
                  pname = "crossmacro-daemon";

                  projectFile = "src/CrossMacro.Daemon/CrossMacro.Daemon.csproj";

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
                    autoPatchelfHook
                  ];

                  buildInputs = with pkgs; [
                    systemd
                  ];

                  dotnetFlags = [
                    "-p:Version=${crossmacroVersion}"
                  ];

                  # Install polkit policy file
                  postInstall = ''
                    install -Dm644 scripts/assets/io.github.alper_han.crossmacro.policy $out/share/polkit-1/actions/io.github.alper_han.crossmacro.policy
                    install -Dm644 scripts/assets/50-crossmacro.rules $out/share/polkit-1/rules.d/50-crossmacro.rules
                    
                    # Force dependency on libsystemd for runtime P/Invoke resolution
                    # This tells autoPatchelfHook to link systemd even though it's not a build-time dep
                    if [ -f $out/lib/crossmacro-daemon/CrossMacro.Daemon ]; then
                       patchelf --add-needed libsystemd.so.0 $out/lib/crossmacro-daemon/CrossMacro.Daemon
                    fi
                  '';

                  meta = with pkgs.lib; {
                    description = "Privileged Daemon for CrossMacro";
                    platforms = platforms.linux;
                    mainProgram = "CrossMacro.Daemon";
                    maintainers = with maintainers; [ ];
                  };
                }
              )
            else
              null;

          # The main CrossMacro package
          crossmacro = pkgs.buildDotnetModule (
            commonDotnetModule
            // {
              pname = "crossmacro";

              projectFile = uiHostProject;

              # .NET 10 Preview support
              dotnet-runtime = pkgs.dotnet-runtime_10;

              executables = [ "CrossMacro.UI" ];

              buildType = "Release";

              # Disable self-contained to use system runtime
              dotnetFlags = [
                "-p:SelfContained=false"
                "-p:Version=${crossmacroVersion}"
              ];

              # Runtime dependencies for Avalonia/SkiaSharp
              runtimeDeps = runtimeLibs;

              postInstall =
                if pkgs.stdenv.isLinux then
                  ''
                    install -Dm644 scripts/assets/CrossMacro.desktop $out/share/applications/crossmacro.desktop
                    
                    # Create lowercase alias for compatibility (and desktop file support)
                    mkdir -p $out/bin
                    ln -s $out/bin/CrossMacro.UI $out/bin/crossmacro
                    
                    ${pkgs.lib.concatMapStringsSep "\n" (size: ''
                      mkdir -p $out/share/icons/hicolor/${size}x${size}/apps
                      install -Dm644 src/CrossMacro.UI/Assets/icons/${size}x${size}/apps/crossmacro.png $out/share/icons/hicolor/${size}x${size}/apps/crossmacro.png
                    '') [ "16" "32" "48" "64" "128" "256" "512" ]}

                    install -Dm644 scripts/assets/io.github.alper-han.CrossMacro.metainfo.xml $out/share/metainfo/io.github.alper-han.CrossMacro.metainfo.xml
                  ''
                else
                  # macOS specific post-install could go here (e.g. bundle creation)
                  # For now, we leave it empty for raw binary output
                  "";

              meta = with pkgs.lib; {
                description = "Cross-platform mouse and keyboard macro recorder and player";
                homepage = "https://github.com/alper-han/CrossMacro";
                license = licenses.gpl3Plus;
                 # Support both Linux and Darwin
                platforms = platforms.unix;
                mainProgram = "crossmacro";
                maintainers = with maintainers; [ ];
              };
            }
          );
        in
        {
          packages =
            {
              default = crossmacro;
              crossmacro = crossmacro;
            }
            // (pkgs.lib.optionalAttrs pkgs.stdenv.isLinux {
              daemon = crossmacro-daemon;
            });

          apps = {
            default = {
              type = "app";
              program = pkgs.lib.getExe crossmacro;
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
              ${pkgs.lib.optionalString pkgs.stdenv.isLinux "echo \"Systemd service required for Input Access.\""}
              echo ""
              echo "Commands:"
              echo "  dotnet run --project ${uiHostProject}"
              echo "  dotnet build"
              echo ""
            '';
          };

          formatter = pkgs.nixfmt-rfc-style;
        };

      flake = {
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
            # Safe access to daemon package, falls back to null if not available
            daemonPkg = 
              if inputs.self.packages.${pkgs.stdenv.hostPlatform.system} ? daemon 
              then inputs.self.packages.${pkgs.stdenv.hostPlatform.system}.daemon
              else null;
          in
          {
            options.programs.crossmacro = {
              enable = mkEnableOption "CrossMacro mouse macro recorder";

              package = mkOption {
                type = types.package;
                default = inputs.self.packages.${pkgs.stdenv.hostPlatform.system}.default;
                description = "Specifies the CrossMacro UI package to be installed.";
              };

              daemonPackage = mkOption {
                type = types.package;
                default = daemonPkg;
                description = "Specifies the CrossMacro Daemon package to be used for privileged operations.";
              };

              users = mkOption {
                type = types.listOf types.str;
                default = [ ];
                description = "List of users granted permission to interact with the CrossMacro daemon (adds them to the 'crossmacro' group).";
              };
            };

            config = mkIf cfg.enable {
              assertions = [
                {
                  assertion = cfg.users != [ ];
                  message = "CrossMacro: You must configure at least one user to access the input daemon.\n       Please set `programs.crossmacro.users = [ \"yourusername\" ];` in your NixOS configuration.";
                }
                # Ensure we are on Linux if enabling the NixOS module (redundant but safe)
                {
                  assertion = pkgs.stdenv.isLinux;
                  message = "CrossMacro NixOS module is only supported on Linux.";
                }
              ];

              environment.systemPackages = [ cfg.package ];

              # Enable uinput for virtual input device creation (required for playback)
              hardware.uinput.enable = true;

              # Fix uinput permissions - NixOS default uses ACLs but group perms are ---
              # This ensures the input group has read/write access
              # Also disable mouse acceleration for CrossMacro virtual device (flat profile)
              services.udev.extraRules = ''
                KERNEL=="uinput", GROUP="input", MODE="0660", OPTIONS+="static_node=uinput"
                ACTION=="add|change", KERNEL=="event*", ATTRS{name}=="CrossMacro Virtual Input Device", ENV{LIBINPUT_ATTR_POINTER_ACCEL}="0"
              '';

              # Install canonical polkit policy for authorization dialogs
              environment.etc."polkit-1/actions/io.github.alper_han.crossmacro.policy".source = "${cfg.daemonPackage}/share/polkit-1/actions/io.github.alper_han.crossmacro.policy";
              
              # Install polkit rules for passwordless auth (local active sessions only)
              environment.etc."polkit-1/rules.d/50-crossmacro.rules".source = "${cfg.daemonPackage}/share/polkit-1/rules.d/50-crossmacro.rules";

              users.groups.crossmacro = { };

              # Add specified users to the crossmacro group and define the daemon user
              users.users =
                builtins.listToAttrs (map (user: {
                  name = user;
                  value = {
                    extraGroups = [ "crossmacro" ];
                  };
                }) cfg.users)
                // {
                  crossmacro = {
                    isSystemUser = true;
                    group = "input";
                    extraGroups = [
                      "crossmacro"
                      "uinput"
                    ];
                    description = "CrossMacro Input Daemon User";
                  };
                };

              systemd.services.crossmacro = {
                description = "CrossMacro Input Daemon Service";
                wantedBy = [ "multi-user.target" ];
                after = [
                  "network.target"
                  "dbus.service"
                  "polkit.service"
                ];
                wants = [
                  "dbus.service"
                  "polkit.service"
                ];
                path = [ pkgs.polkit ]; # For pkcheck command
                serviceConfig = {
                  Type = "notify";
                  User = "crossmacro";
                  Group = "input";
                  ExecStart = "${lib.getExe cfg.daemonPackage}";
                  Restart = "always";
                  RestartSec = 5;
                  RuntimeDirectory = "crossmacro";
                  RuntimeDirectoryMode = "0755";
                  CapabilityBoundingSet = [ "CAP_SYS_ADMIN" "CAP_SETUID" "CAP_SETGID" "CAP_CHOWN" "CAP_DAC_READ_SEARCH" ];
                  AmbientCapabilities = [ "CAP_SYS_ADMIN" "CAP_CHOWN" "CAP_DAC_READ_SEARCH" ];
                };
              };
            };
          };
      };
    };
}
