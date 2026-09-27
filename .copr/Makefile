SHELL := /bin/bash
outdir ?= artifacts/packages/srpm
spec ?= scripts/packaging/rpm/crossmacro.spec

.PHONY: srpm
srpm:
	@missing=(); \
	for pair in rpmbuild:rpm-build git:git tar:tar gzip:gzip realpath:coreutils sed:sed; do \
	    command -v "$${pair%%:*}" >/dev/null 2>&1 || missing+=("$${pair#*:}"); \
	done; \
	if [ "$${#missing[@]}" -ne 0 ]; then \
	    command -v dnf >/dev/null 2>&1 && [ "$$(id -u)" -eq 0 ] || { echo "Missing SRPM tools: $${missing[*]}" >&2; exit 1; }; \
	    dnf -y install "$${missing[@]}"; \
	fi; \
	bash scripts/packaging/rpm/build-srpm.sh --outdir "$(outdir)" --spec "$(spec)"
