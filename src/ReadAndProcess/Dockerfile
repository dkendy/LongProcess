FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY src/ReadAndProcess/ReadAndProcess.csproj ReadAndProcess/ReadAndProcess.csproj
COPY src/consumer/consumer.csproj consumer/consumer.csproj
COPY src/producer/producer.csproj producer/producer.csproj

RUN dotnet restore ReadAndProcess/ReadAndProcess.csproj

COPY src/ReadAndProcess/ ReadAndProcess/
COPY src/consumer/ consumer/
COPY src/producer/ producer/

RUN dotnet publish -c Release -o /app/out ReadAndProcess/ReadAndProcess.csproj

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/out .

EXPOSE 80

ENTRYPOINT ["dotnet", "ReadAndProcess.dll"]