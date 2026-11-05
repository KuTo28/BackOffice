FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /src

COPY ["AltairisBackoffice.Api/AltairisBackoffice.Api.csproj", "AltairisBackoffice.Api/"]
COPY ["AltairisBackoffice.Api.Tests/AltairisBackoffice.Api.Tests.csproj", "AltairisBackoffice.Api.Tests/"]
RUN dotnet restore "AltairisBackoffice.Api.Tests/AltairisBackoffice.Api.Tests.csproj"

COPY AltairisBackoffice.Api/ AltairisBackoffice.Api/
COPY AltairisBackoffice.Api.Tests/ AltairisBackoffice.Api.Tests/

ENTRYPOINT ["dotnet", "test", "AltairisBackoffice.Api.Tests/AltairisBackoffice.Api.Tests.csproj", "--no-restore"]
