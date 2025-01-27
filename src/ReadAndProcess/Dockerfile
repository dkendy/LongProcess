FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY src/ReadAndProcess/ReadAndProcess.csproj ReadAndProcess/ReadAndProcess.csproj
COPY src/consumer/consumer.csproj consumer/consumer.csproj
COPY src/producer/producer.csproj producer/producer.csproj
COPY src/Infrastructure/Infrastructure.csproj Infrastructure/Infrastructure.csproj
COPY src/makefile/makefile.csproj makefile/makefile.csproj
COPY src/consumerxml/consumerxml.csproj consumerxml/consumerxml.csproj
COPY LongProcess.sln ../LongProcess.sln
COPY Directory.Build.props ../Directory.Build.props
COPY .editorconfig ../.editorconfig

RUN dotnet restore ../LongProcess.sln

COPY src/ReadAndProcess/ ReadAndProcess/
COPY src/consumer/ consumer/
COPY src/producer/ producer/
COPY src/Infrastructure/ Infrastructure/


RUN dotnet publish -c Release -o /app/out ReadAndProcess/ReadAndProcess.csproj

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/out .

EXPOSE 80

ENTRYPOINT ["dotnet", "ReadAndProcess.dll"]