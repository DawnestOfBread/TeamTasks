FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["TeamTasks.Api/TeamTasks.Api.csproj", "TeamTasks.Api/"]
COPY ["TeamTasks.Application/TeamTasks.Application.csproj", "TeamTasks.Application/"]
COPY ["TeamTasks.Domain/TeamTasks.Domain.csproj", "TeamTasks.Domain/"]
RUN dotnet restore "TeamTasks.Api/TeamTasks.Api.csproj"
COPY . .
WORKDIR "/src/TeamTasks.Api"
RUN dotnet build "./TeamTasks.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./TeamTasks.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TeamTasks.Api.dll"]
