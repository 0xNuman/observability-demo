FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["ObservabilityDemo.slnx", "./"]
COPY ["src/ObservabilityDemo.Api/ObservabilityDemo.Api.csproj", "src/ObservabilityDemo.Api/"]
COPY ["src/ObservabilityDemo.Application/ObservabilityDemo.Application.csproj", "src/ObservabilityDemo.Application/"]
COPY ["src/ObservabilityDemo.Domain/ObservabilityDemo.Domain.csproj", "src/ObservabilityDemo.Domain/"]
COPY ["src/ObservabilityDemo.Infrastructure/ObservabilityDemo.Infrastructure.csproj", "src/ObservabilityDemo.Infrastructure/"]

RUN dotnet restore "src/ObservabilityDemo.Api/ObservabilityDemo.Api.csproj"

COPY . .
RUN dotnet publish "src/ObservabilityDemo.Api/ObservabilityDemo.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ObservabilityDemo.Api.dll"]
