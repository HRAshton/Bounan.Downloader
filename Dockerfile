﻿FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base

VOLUME /tmp/bounan-downloader

RUN apt-get update \
    && apt-get install -y ffmpeg fonts-roboto \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["src/Common/StyleCop.props", "Common/"]
COPY ["src/Common/cs/Common.csproj", "Common/"]
COPY ["src/LoanApi/StyleCop.props", "LoanApi/"]
COPY ["src/LoanApi/LoanApi/LoanApi.csproj", "LoanApi/LoanApi/"]
COPY ["src/Worker/Worker.csproj", "Worker/"]
RUN dotnet restore "Worker/Worker.csproj"

COPY src .
WORKDIR /src/Worker
RUN dotnet build "Worker.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Worker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final

WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "Bounan.Downloader.Worker.dll"]
