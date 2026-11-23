# 1. Imagen base con .NET SDK para compilar
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# 2. Copiamos el archivo del proyecto y restauramos dependencias
COPY *.csproj ./
RUN dotnet restore

# 3. Copiamos todo el código y compilamos
COPY . ./
RUN dotnet publish BackEnd_Gps.csproj -c Release -o out

# 4. Imagen final más liviana solo para ejecutar
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# 5. Copiamos el resultado compilado
COPY --from=build /app/out .

# The API does not need root privileges. Keep writable application state in a
# dedicated home directory; production should mount the protected Data Protection key ring there.
RUN addgroup --system --gid 10001 fletflow \
    && adduser --uid 10001 --ingroup fletflow --home /home/fletflow --disabled-password --gecos "" fletflow \
    && mkdir -p /home/fletflow/.aspnet/DataProtection-Keys \
    && chown -R fletflow:fletflow /home/fletflow
ENV HOME=/home/fletflow
USER fletflow

# 6. Puerto que usa tu API
EXPOSE 5046

# 7. Comando para iniciar la API
ENTRYPOINT ["dotnet", "BackEnd_Gps.dll"]
