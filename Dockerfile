# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0.403-noble AS build
WORKDIR /source
COPY global.json ./
COPY src/Cube.Api/Cube.Api.csproj src/Cube.Api/packages.lock.json src/Cube.Api/
# Optional CA bundle for development environments with an HTTPS proxy; never copied into the image.
RUN --mount=type=secret,id=proxy_ca \
    if [ -f /run/secrets/proxy_ca ]; then export SSL_CERT_FILE=/run/secrets/proxy_ca; fi; \
    dotnet restore src/Cube.Api/Cube.Api.csproj --locked-mode
COPY src/Cube.Api/ src/Cube.Api/
RUN dotnet publish src/Cube.Api/Cube.Api.csproj --configuration Release --no-restore \
    --output /publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.14-noble AS runtime
RUN --mount=type=secret,id=proxy_ca \
    if [ -f /run/secrets/proxy_ca ]; then \
        printf 'Acquire::https::CaInfo "/run/secrets/proxy_ca";\n' > /etc/apt/apt.conf.d/99cube-build-ca; \
    fi; \
    apt-get update && apt-get install -y --no-install-recommends tesseract-ocr curl \
    && rm -rf /var/lib/apt/lists/* /etc/apt/apt.conf.d/99cube-build-ca
WORKDIR /app
COPY --from=build /publish ./
RUN mkdir -p /var/lib/cube/files /home/app/.aspnet/DataProtection-Keys \
    && chown -R app:app /var/lib/cube /home/app/.aspnet \
    && chmod 700 /home/app/.aspnet/DataProtection-Keys
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    Files__Root=/var/lib/cube/files
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Cube.Api.dll"]
