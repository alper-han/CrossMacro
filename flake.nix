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
        "aarch64-darwin"
      ];

      perSystem =
        {
          pkgs,
          system,
          ...
        }:
        let
          versionFileContent = builtins.readFile ./VERSION;
          normalizedVersion =
            builtins.replaceStrings [ "\n" "\r" " " "\t" ] [ "" "" "" "" ]
              versionFileContent;
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
            libxtst

            # GLib for GIO
            glib

            # Graphics/OpenGL
            libglvnd

            # Wayland support
            wayland
            libxkbcommon

            # Wayland screen reading backend
            pipewire
          ];

          # Runtime libraries
          runtimeLibs = commonLibs ++ (if pkgs.stdenv.hostPlatform.isLinux then linuxLibs else [ ]);
          uiHostProject =
            if pkgs.stdenv.hostPlatform.isDarwin then
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
            if pkgs.stdenv.hostPlatform.isLinux then
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

                  useAppHost = false;

                  # Native AOT requires clang for compilation and patching
                  nativeBuildInputs = with pkgs; [
                    clang
                    autoPatchelfHook
                    patchelf
                  ];

                  buildInputs = with pkgs; [
                    systemdLibs
                    zlib
                  ];

                  dotnetFlags = [
                    "-p:CrossMacroPublishProfile=native-aot"
                    "-p:Version=${crossmacroVersion}"
                  ];

                  # Install polkit policy file
                  postInstall = ''
                    install -Dm644 scripts/assets/io.github.alper_han.crossmacro.policy $out/share/polkit-1/actions/io.github.alper_han.crossmacro.policy
                    install -Dm644 scripts/assets/50-crossmacro.rules $out/share/polkit-1/rules.d/50-crossmacro.rules

                    # Keep the Native AOT libsystemd dependency explicit.
                    patchelf --add-needed libsystemd.so.0 $out/lib/crossmacro-daemon/CrossMacro.Daemon
                  '';

                  meta = with pkgs.lib; {
                    description = "Privileged input daemon for CrossMacro";
                    homepage = "https://github.com/alper-han/CrossMacro";
                    license = licenses.gpl3Only;
                    platforms = platforms.linux;
                    mainProgram = "CrossMacro.Daemon";
                    maintainers = with maintainers; [ alper-han ];
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

              # Native AOT is self-contained, no runtime needed
              dotnet-runtime = null;

              executables = [ "CrossMacro.UI" ];

              buildType = "Release";

              # Enable self-contained build for Native AOT
              selfContainedBuild = true;

              useAppHost = false;

              dotnetFlags = [
                "-p:CrossMacroPublishProfile=native-aot"
                "-p:Version=${crossmacroVersion}"
              ];

              # Runtime dependencies for Avalonia/SkiaSharp
              runtimeDeps = runtimeLibs;

              buildInputs = pkgs.lib.optionals pkgs.stdenv.hostPlatform.isLinux runtimeLibs;

              nativeBuildInputs = [
                pkgs.installShellFiles
              ]
              ++ pkgs.lib.optionals pkgs.stdenv.hostPlatform.isLinux [
                pkgs.clang
                pkgs.autoPatchelfHook
              ];

              postInstall = ''
                installManPage docs/man/crossmacro.1
              ''
              + (
                if pkgs.stdenv.hostPlatform.isLinux then
                  ''
                    install -Dm644 scripts/assets/io.github.alper_han.crossmacro.desktop $out/share/applications/io.github.alper_han.crossmacro.desktop
                    substituteInPlace $out/share/applications/io.github.alper_han.crossmacro.desktop \
                      --replace-fail "Exec=crossmacro" "Exec=$out/lib/crossmacro/CrossMacro.UI"

                    ${pkgs.lib.concatMapStringsSep "\n"
                      (size: ''
                        mkdir -p $out/share/icons/hicolor/${size}x${size}/apps
                        install -Dm644 src/CrossMacro.UI/Assets/icons/${size}x${size}/apps/crossmacro.png $out/share/icons/hicolor/${size}x${size}/apps/crossmacro.png
                      '')
                      [
                        "16"
                        "32"
                        "48"
                        "64"
                        "128"
                        "256"
                        "512"
                      ]
                    }

                    install -Dm644 scripts/assets/io.github.alper_han.crossmacro.metainfo.xml $out/share/metainfo/io.github.alper_han.crossmacro.metainfo.xml
                  ''
                else
                  # macOS specific post-install could go here (e.g. bundle creation)
                  # For now, we leave it empty for raw binary output
                  ""
              );

              # Keep desktop Exec aligned with /proc/<pid>/exe for KWin's
              # restricted screenshot permission checks.
              postFixup = pkgs.lib.optionalString pkgs.stdenv.hostPlatform.isLinux ''
                # Align wrapper and ELF paths so KWin's strict /proc/<pid>/exe check grants Wayland permissions.
                # Move the real ELF binary to .CrossMacro.UI-wrapped
                mv $out/lib/crossmacro/CrossMacro.UI \
                   $out/lib/crossmacro/.CrossMacro.UI-wrapped

                # Move the buildDotnetModule wrapper from bin/ into lib/ so
                # its path matches what KWin resolves after unwrapping.
                mv $out/bin/CrossMacro.UI $out/lib/crossmacro/CrossMacro.UI

                # Update the wrapper's exec target to the renamed binary
                substituteInPlace $out/lib/crossmacro/CrossMacro.UI \
                  --replace-fail \
                    "\"$out/lib/crossmacro/CrossMacro.UI\"" \
                    "\"$out/lib/crossmacro/.CrossMacro.UI-wrapped\""

                # Point bin/ entries at the lib/ wrapper
                rm -f $out/bin/crossmacro
                ln -s $out/lib/crossmacro/CrossMacro.UI $out/bin/CrossMacro.UI
                ln -s $out/bin/CrossMacro.UI $out/bin/crossmacro
              '';

              meta = with pkgs.lib; {
                description = "Mouse and keyboard macro recorder and automation with a macro editor, hotkeys, scheduling, text expansion, screen recognition, and CLI control";
                homepage = "https://github.com/alper-han/CrossMacro";
                license = licenses.gpl3Only;
                platforms = [
                  "x86_64-linux"
                  "aarch64-linux"
                  "aarch64-darwin"
                ];
                mainProgram = "crossmacro";
                maintainers = with maintainers; [ alper-han ];
              };
            }
          );
        in
        {
          packages = {
            default = crossmacro;
            crossmacro = crossmacro;
          }
          // (pkgs.lib.optionalAttrs pkgs.stdenv.hostPlatform.isLinux {
            crossmacro-daemon = crossmacro-daemon;
          });

          apps = {
            default = {
              type = "app";
              program = pkgs.lib.getExe crossmacro;
              meta.description = crossmacro.meta.description;
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
              ${pkgs.lib.optionalString pkgs.stdenv.hostPlatform.isLinux "echo \"Linux input can use either daemon-backed mode or direct device mode depending on how you launch CrossMacro.\""}
              echo ""
              echo "Commands:"
              echo "  dotnet run --project ${uiHostProject}"
              echo "  dotnet build"
              echo ""
            '';
          };

          checks = pkgs.lib.optionalAttrs pkgs.stdenv.hostPlatform.isLinux {
            crossmacro-nixos-userborn-directory-identities =
              let
                testUiPackage = pkgs.writeShellScriptBin "crossmacro-test-ui" "exit 0";
                testDaemonPackage = pkgs.runCommand "crossmacro-test-daemon-package" { } ''
                  install -Dm755 ${pkgs.writeShellScript "crossmacro-test-daemon" "exit 0"} "$out/bin/crossmacro-test-daemon"
                  install -Dm644 ${./scripts/assets/io.github.alper_han.crossmacro.policy} "$out/share/polkit-1/actions/io.github.alper_han.crossmacro.policy"
                  install -Dm644 ${./scripts/assets/50-crossmacro.rules} "$out/share/polkit-1/rules.d/50-crossmacro.rules"
                '';
                testSystem = inputs.nixpkgs.lib.nixosSystem {
                  inherit system;
                  modules = [
                    inputs.self.nixosModules.default
                    {
                      system.stateVersion = "25.11";
                      users.users.local-user = {
                        isSystemUser = true;
                        group = "users";
                      };
                      services.crossmacro = {
                        enable = true;
                        package = testUiPackage;
                        daemonPackage = testDaemonPackage;
                        users = [
                          "local-user"
                          "directory-user"
                        ];
                      };
                    }
                  ];
                };
              in
              assert testSystem.config.services.userborn.enable;
              assert !testSystem.config.systemd.sysusers.enable;
              assert !(testSystem.config.users.users ? "directory-user");
              assert builtins.elem "local-user" testSystem.config.users.groups.crossmacro.members;
              assert builtins.elem "directory-user" testSystem.config.users.groups.crossmacro.members;
              pkgs.runCommand "crossmacro-nixos-userborn-directory-identities-check" { } ''
                touch "$out"
              '';
          };

          formatter = pkgs.nixfmt;
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
            cfg = config.services.crossmacro;
            daemonPkg = inputs.self.packages.${pkgs.stdenv.hostPlatform.system}.crossmacro-daemon;
          in
          {
            # Replace nixpkgs' module to keep directory identities out of local users.
            disabledModules = [ "services/desktops/crossmacro.nix" ];

            options.services.crossmacro = {
              enable = mkEnableOption "CrossMacro, a cross-platform mouse and keyboard macro application";

              package = mkOption {
                type = types.package;
                default = inputs.self.packages.${pkgs.stdenv.hostPlatform.system}.crossmacro;
                description = "CrossMacro UI package.";
              };

              daemonPackage = mkOption {
                type = types.package;
                default = daemonPkg;
                description = "CrossMacro input daemon package.";
              };

              users = mkOption {
                type = types.listOf types.str;
                default = [ ];
                example = [
                  "alice"
                  "bob"
                ];
                description = "Local or directory-service identities granted access to CrossMacro.";
              };
            };

            config = mkIf cfg.enable {
              assertions = [
                {
                  assertion = cfg.users != [ ];
                  message = "CrossMacro: configure at least one identity with `services.crossmacro.users`.";
                }
                {
                  assertion = config.services.userborn.enable && !config.systemd.sysusers.enable;
                  message = "CrossMacro: Userborn is required and cannot be combined with systemd-sysusers.";
                }
              ];

              environment.systemPackages = [ cfg.package ];

              # Enable uinput for virtual input device creation (required for playback)
              hardware.uinput.enable = true;

              # Keep NSS identities out of the local user database.
              services.userborn.enable = mkDefault true;

              # Ensure uinput access and disable acceleration for the virtual pointer.
              services.udev.extraRules = ''
                KERNEL=="uinput", GROUP="input", MODE="0660", OPTIONS+="static_node=uinput"
                ACTION=="add|change", KERNEL=="event*", ATTRS{name}=="CrossMacro Virtual Input Device", ENV{LIBINPUT_ATTR_POINTER_ACCEL}="0"
              '';

              # Install canonical polkit policy for authorization dialogs
              environment.etc."polkit-1/actions/io.github.alper_han.crossmacro.policy".source =
                "${cfg.daemonPackage}/share/polkit-1/actions/io.github.alper_han.crossmacro.policy";

              # Install polkit rules for passwordless auth (local active sessions only)
              environment.etc."polkit-1/rules.d/50-crossmacro.rules".source =
                "${cfg.daemonPackage}/share/polkit-1/rules.d/50-crossmacro.rules";

              # Keep configured identities as group members instead of local users.
              users.groups.crossmacro.members = cfg.users;

              users.users.crossmacro = {
                isSystemUser = true;
                group = "crossmacro";
                extraGroups = [
                  "input"
                  "uinput"
                ];
                description = "CrossMacro Input Daemon User";
              };

              systemd.services.crossmacro = {
                description = "CrossMacro Input Daemon Service";
                documentation = [ "https://github.com/alper-han/CrossMacro" ];
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
                  Group = "crossmacro";
                  ExecStart = "${lib.getExe cfg.daemonPackage}";
                  Restart = "always";
                  RestartSec = 5;
                  RuntimeDirectory = "crossmacro";
                  RuntimeDirectoryMode = "0750";
                  CapabilityBoundingSet = [
                    "CAP_SYS_ADMIN"
                    "CAP_SETUID"
                    "CAP_SETGID"
                    "CAP_CHOWN"
                    "CAP_DAC_READ_SEARCH"
                  ];
                  AmbientCapabilities = [
                    "CAP_SYS_ADMIN"
                    "CAP_CHOWN"
                    "CAP_DAC_READ_SEARCH"
                  ];
                };
              };
            };

            meta.maintainers = with maintainers; [ alper-han ];
          };
      };
    };
}
