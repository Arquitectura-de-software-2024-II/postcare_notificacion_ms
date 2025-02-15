# Use the official .NET runtime image as a base
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

# Use the official .NET SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the project files and restore dependencies
COPY postcare_notificacion_ms.csproj ./
RUN dotnet restore ./postcare_notificacion_ms.csproj

# Copy the rest of the source code and build the project
COPY . .
WORKDIR /src
RUN dotnet publish -c Release -o /app/publish

# Build the runtime image
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

#COPY
COPY .env .env

# Run the worker service
ENTRYPOINT ["dotnet", "postcare_notificacion_ms.dll"]