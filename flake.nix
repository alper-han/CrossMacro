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
          inherit (pkgs) lib;
          isLinux = pkgs.stdenv.hostPlatform.isLinux;
          canRunHostPlatform = pkgs.stdenv.buildPlatform.canExecute pkgs.stdenv.hostPlatform;
          crossmacroVersion =
            let
              version = lib.strings.trim (builtins.readFile ./VERSION);
            in
            if builtins.match "[0-9]+\\.[0-9]+\\.[0-9]+" version == null then
              throw "Invalid VERSION file format. Expected X.Y.Z"
            else
              version;
          nativeDesktopId = "CrossMacro.desktop";
          uiExecutableName = "CrossMacro.UI";
          uiExecutablePath = "lib/crossmacro/${uiExecutableName}";
          iconSizes = [
            "16"
            "32"
            "48"
            "64"
            "128"
            "256"
            "512"
          ];

          commonLibs = with pkgs; [
            zlib
            icu
            openssl
          ];

          linuxLibs = with pkgs; [
            fontconfig
            freetype
            expat
            libx11
            libice
            libsm
            libxi
            libxcursor
            libxext
            libxrandr
            libxtst
            glib
            libglvnd
            wayland
            libxkbcommon
            pipewire
          ];

          runtimeLibs = map lib.getLib (commonLibs ++ lib.optionals isLinux linuxLibs);
          uiHostProject =
            if pkgs.stdenv.hostPlatform.isDarwin then
              "src/CrossMacro.UI.MacOS/CrossMacro.UI.MacOS.csproj"
            else
              "src/CrossMacro.UI.Linux/CrossMacro.UI.Linux.csproj";

          commonDotnetAttrs = {
            pname = "crossmacro";
            version = crossmacroVersion;
            src = ./.;
            nugetDeps = ./deps.json;
            dotnet-sdk = pkgs.dotnet-sdk_10;
          };

          nativeAotDotnetAttrs = finalAttrs: {
            dotnet-runtime = null;
            buildType = "Release";
            selfContainedBuild = true;
            useAppHost = false;
            dotnetFlags = [
              "-p:CrossMacroPublishProfile=native-aot"
              "-p:Version=${finalAttrs.version}"
            ];
          };

          commonMeta = {
            homepage = "https://github.com/alper-han/CrossMacro";
            license = lib.licenses.gpl3Only;
            maintainers = [ lib.maintainers."alper-han" ];
          };

          crossmacro-daemon =
            if isLinux then
              pkgs.buildDotnetModule (
                finalAttrs:
                commonDotnetAttrs
                // (nativeAotDotnetAttrs finalAttrs)
                // {
                  pname = "crossmacro-daemon";

                  projectFile = "src/CrossMacro.Daemon/CrossMacro.Daemon.csproj";
                  executables = [ "CrossMacro.Daemon" ];
                  nativeBuildInputs = with pkgs; [
                    clang
                    autoPatchelfHook
                  ];

                  buildInputs = with pkgs; [
                    systemdLibs
                    zlib
                  ];

                  runtimeDependencies = [ pkgs.systemdLibs ];

                  postInstall = ''
                    install -Dm644 scripts/assets/io.github.alper_han.crossmacro.policy $out/share/polkit-1/actions/io.github.alper_han.crossmacro.policy
                    install -Dm644 scripts/assets/50-crossmacro.rules $out/share/polkit-1/rules.d/50-crossmacro.rules
                  '';

                  meta = commonMeta // {
                    description = "Privileged input daemon for CrossMacro";
                    platforms = lib.platforms.linux;
                    mainProgram = "CrossMacro.Daemon";
                  };
                }
              )
            else
              null;

          crossmacro = pkgs.buildDotnetModule (
            finalAttrs:
            commonDotnetAttrs
            // (nativeAotDotnetAttrs finalAttrs)
            // {
              pname = "crossmacro";

              projectFile = uiHostProject;
              executables = if isLinux then [ ] else [ uiExecutableName ];
              buildInputs = lib.optionals isLinux runtimeLibs;
              runtimeDependencies = lib.optionals isLinux runtimeLibs;

              nativeBuildInputs = [
                pkgs.installShellFiles
              ]
              ++ lib.optionals isLinux [
                pkgs.clang
                pkgs.autoPatchelfHook
              ];

              postInstall = ''
                installManPage docs/man/crossmacro.1
              ''
              + lib.optionalString isLinux ''
                install -Dm644 scripts/assets/${nativeDesktopId} $out/share/applications/${nativeDesktopId}
                substituteInPlace $out/share/applications/${nativeDesktopId} \
                  --replace-fail "Exec=crossmacro" "Exec=$out/${uiExecutablePath}"

                ${lib.concatMapStringsSep "\n" (size: ''
                  mkdir -p $out/share/icons/hicolor/${size}x${size}/apps
                  install -Dm644 src/CrossMacro.UI/Assets/icons/${size}x${size}/apps/crossmacro.png $out/share/icons/hicolor/${size}x${size}/apps/crossmacro.png
                '') iconSizes}

                install -Dm644 scripts/assets/io.github.alper_han.crossmacro.metainfo.xml $out/share/metainfo/io.github.alper_han.crossmacro.metainfo.xml
                substituteInPlace $out/share/metainfo/io.github.alper_han.crossmacro.metainfo.xml \
                  --replace-fail "<launchable type=\"desktop-id\">io.github.alper_han.crossmacro.desktop</launchable>" "<launchable type=\"desktop-id\">${nativeDesktopId}</launchable>"

                mkdir -p $out/bin
                ln -s ../${uiExecutablePath} $out/bin/${uiExecutableName}
                ln -s ${uiExecutableName} $out/bin/crossmacro
              '';

              meta = commonMeta // {
                description = "Mouse and keyboard macro recorder and automation with a macro editor, hotkeys, scheduling, text expansion, screen recognition, and CLI control";
                platforms = [
                  "x86_64-linux"
                  "aarch64-linux"
                  "aarch64-darwin"
                ];
                mainProgram = "crossmacro";
              };
            }
          );
        in
        {
          packages = {
            inherit crossmacro;
            default = crossmacro;
          }
          // (lib.optionalAttrs isLinux {
            inherit crossmacro-daemon;
          });

          apps = {
            default = {
              type = "app";
              program = lib.getExe crossmacro;
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

            LD_LIBRARY_PATH = "${lib.makeLibraryPath runtimeLibs}";

            shellHook = ''
              echo "CrossMacro Development Environment"
              echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
              echo "Dotnet SDK: $(dotnet --version)"
              ${lib.optionalString isLinux "echo \"Linux input can use either daemon-backed mode or direct device mode depending on how you launch CrossMacro.\""}
              echo ""
              echo "Commands:"
              echo "  dotnet run --project ${uiHostProject}"
              echo "  dotnet build"
              echo ""
            '';
          };

          checks = lib.optionalAttrs (isLinux && canRunHostPlatform) {
            crossmacro-kde-desktop-executable-identity = pkgs.testers.runCommand {
              name = "crossmacro-kde-desktop-executable-identity-check";
              nativeBuildInputs = [
                pkgs.desktop-file-utils
                pkgs.file
                pkgs.appstream
              ];
              script = ''
                desktop=${crossmacro}/share/applications/${nativeDesktopId}
                executable=${crossmacro}/${uiExecutablePath}

                desktop-file-validate "$desktop"
                grep -Fx "Exec=$executable" "$desktop"
                grep -Fx "X-KDE-DBUS-Restricted-Interfaces=org.kde.KWin.ScreenShot2" "$desktop"
                test -x "$executable"
                file -L "$executable" | grep -q 'ELF'
                test "$(readlink -f ${crossmacro}/bin/crossmacro)" = "$(readlink -f "$executable")"
                test "$(readlink -f ${crossmacro}/bin/CrossMacro.UI)" = "$(readlink -f "$executable")"
                env -i HOME="$TMPDIR" "$executable" --version | grep -F "${uiExecutableName} ${crossmacroVersion}"
                appstreamcli validate-tree --no-net ${crossmacro}

                touch "$out"
              '';
            };

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
              pkgs.testers.runCommand {
                name = "crossmacro-nixos-userborn-directory-identities-check";
                script = "touch $out";
              };
          };

          formatter = pkgs.nixfmt-tree;
        };

      flake = {
        nixosModules.default =
          {
            config,
            lib,
            pkgs,
            ...
          }:
          let
            cfg = config.services.crossmacro;
            packageSet = inputs.self.packages.${pkgs.stdenv.hostPlatform.system};
          in
          {
            disabledModules = [ "services/desktops/crossmacro.nix" ];

            options.services.crossmacro = {
              enable = lib.mkEnableOption "CrossMacro, a cross-platform mouse and keyboard macro application";

              package = lib.mkOption {
                type = lib.types.package;
                default = packageSet.crossmacro;
                defaultText = lib.literalExpression "inputs.self.packages.${pkgs.stdenv.hostPlatform.system}.crossmacro";
                description = "CrossMacro UI package.";
              };

              daemonPackage = lib.mkOption {
                type = lib.types.package;
                default = packageSet.crossmacro-daemon;
                defaultText = lib.literalExpression "inputs.self.packages.${pkgs.stdenv.hostPlatform.system}.crossmacro-daemon";
                description = "CrossMacro input daemon package.";
              };

              users = lib.mkOption {
                type = lib.types.listOf lib.types.str;
                default = [ ];
                example = [
                  "alice"
                  "bob"
                ];
                description = "Local or directory-service identities granted access to CrossMacro.";
              };
            };

            config = lib.mkIf cfg.enable {
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

              environment = {
                systemPackages = [ cfg.package ];

                etc."polkit-1/actions/io.github.alper_han.crossmacro.policy".source =
                  "${cfg.daemonPackage}/share/polkit-1/actions/io.github.alper_han.crossmacro.policy";

                etc."polkit-1/rules.d/50-crossmacro.rules".source =
                  "${cfg.daemonPackage}/share/polkit-1/rules.d/50-crossmacro.rules";
              };

              hardware.uinput.enable = true;
              services.userborn.enable = lib.mkDefault true;
              services.udev.extraRules = ''
                KERNEL=="uinput", GROUP="input", MODE="0660", OPTIONS+="static_node=uinput"
                ACTION=="add|change", KERNEL=="event*", ATTRS{name}=="CrossMacro Virtual Input Device", ENV{LIBINPUT_ATTR_POINTER_ACCEL}="0"
              '';
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
                  "dbus.service"
                  "polkit.service"
                ];
                wants = [
                  "dbus.service"
                  "polkit.service"
                ];
                path = [ pkgs.polkit ];
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

            meta.maintainers = [ lib.maintainers."alper-han" ];
          };
      };
    };
}
