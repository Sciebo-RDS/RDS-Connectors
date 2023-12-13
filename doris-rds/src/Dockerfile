#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["DorisScieboRdsConnector/DorisScieboRdsConnector.csproj", "DorisScieboRdsConnector/"]
RUN dotnet restore "DorisScieboRdsConnector/DorisScieboRdsConnector.csproj"
COPY . .
WORKDIR "/src/DorisScieboRdsConnector"
RUN dotnet build "DorisScieboRdsConnector.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DorisScieboRdsConnector.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DorisScieboRdsConnector.dll"]