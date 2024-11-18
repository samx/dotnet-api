# Stage 1: Build the application
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files
COPY MyFirstAPI.csproj ./
RUN dotnet restore "MyFirstAPI.csproj"

# Copy remaining source code and publish
COPY . .
RUN dotnet publish "MyFirstAPI.csproj" -c Release -o /publish

# Stage 2: Build the runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Copy the published application
COPY --from=build /publish .

# Expose the default port
EXPOSE 80
ENV ASPNETCORE_URLS=http://0.0.0.0:80
# Set the entry point
ENTRYPOINT ["dotnet", "MyFirstAPI.dll"]