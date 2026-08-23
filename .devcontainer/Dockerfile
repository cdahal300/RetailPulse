FROM mcr.microsoft.com/devcontainers/base:bookworm

USER root

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        curl \
        git \
        jq \
        unzip \
        ca-certificates \
        postgresql-client \
        redis-tools \
    && rm -rf /var/lib/apt/lists/*

USER vscode
