FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /TalkingBot

COPY . ./
RUN dotnet restore
RUN dotnet publish -c Release -o dist

ARG TOKEN
ARG GUILDS

# This is a mess but it works. Copy your config file into /TalkingBot/config/ in a container

FROM mcr.microsoft.com/dotnet/aspnet:6.0
WORKDIR /TalkingBot
COPY --from=build-env /TalkingBot/dist .
ENTRYPOINT [ "dotnet", "TalkingBot.dll", "-C", "config/Config.json" ]
