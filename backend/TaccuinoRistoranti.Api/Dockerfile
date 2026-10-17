FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["backend/TaccuinoRistoranti.Api/TaccuinoRistoranti.Api.csproj", "backend/TaccuinoRistoranti.Api/"]
RUN dotnet restore "backend/TaccuinoRistoranti.Api/TaccuinoRistoranti.Api.csproj"

COPY . .
WORKDIR "/src/backend/TaccuinoRistoranti.Api"
RUN dotnet publish "TaccuinoRistoranti.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "TaccuinoRistoranti.Api.dll"]
