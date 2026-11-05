FROM mcr.microsoft.com/powershell:7.4-debian-12

ENV LANG=C.UTF-8 \
    LC_ALL=C.UTF-8

RUN apt-get update \
    && apt-get install -y --no-install-recommends postgresql-client \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /src
COPY AltairisBackoffice.Api/Database/Seed/ /src/AltairisBackoffice.Api/Database/Seed/

ENTRYPOINT ["pwsh", "-NoLogo", "-NoProfile", "-File", "/src/AltairisBackoffice.Api/Database/Seed/ResetAndSeedDemoData.ps1", "-BookingsOnly"]
