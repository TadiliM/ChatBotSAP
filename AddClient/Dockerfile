FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
RUN apt-get update && apt-get install -y libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["AddClient.csproj","."]
RUN dotnet restore "AddClient.csproj"
COPY . .
RUN dotnet publish "AddClient.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build app/publish .
ENTRYPOINT ["dotnet","AddClient.dll"]