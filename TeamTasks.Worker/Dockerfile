FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["TeamTasks.Worker/TeamTasks.Worker.csproj", "TeamTasks.Worker/"]
RUN dotnet restore "TeamTasks.Worker/TeamTasks.Worker.csproj"
COPY . .
WORKDIR "/src/TeamTasks.Worker"
RUN dotnet build "./TeamTasks.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./TeamTasks.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TeamTasks.Worker.dll"]
