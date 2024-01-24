#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

RUN apt-get update \
    && apt-get install -y --no-install-recommends libgdiplus libc6-dev \
    && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["microservice-survey.csproj", "."]
RUN dotnet restore "./microservice-survey.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "microservice-survey.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "microservice-survey.csproj" -c Release -o /app/publish /p:UseAppHost=false


FROM base AS final
WORKDIR /app


RUN apt-get update 
RUN apt-get --yes install curl

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "microservice-survey.dll"]

