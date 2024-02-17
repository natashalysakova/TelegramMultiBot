FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env

WORKDIR /Bober.Worker
COPY . ./

RUN dotnet restore
RUN dotnet publish -c Release




