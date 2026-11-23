FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo
COPY Directory.Build.props stylecop.json .editorconfig ./
COPY src/TeachingAssistant.Domain/TeachingAssistant.Domain.csproj src/TeachingAssistant.Domain/
COPY src/TeachingAssistant.Application/TeachingAssistant.Application.csproj src/TeachingAssistant.Application/
COPY src/TeachingAssistant.Infrastructure/TeachingAssistant.Infrastructure.csproj src/TeachingAssistant.Infrastructure/
COPY src/TeachingAssistant.WebAPI/TeachingAssistant.WebAPI.csproj src/TeachingAssistant.WebAPI/
RUN dotnet restore src/TeachingAssistant.WebAPI/TeachingAssistant.WebAPI.csproj
COPY src/ src/
RUN dotnet publish src/TeachingAssistant.WebAPI/TeachingAssistant.WebAPI.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
COPY --from=docker:29-cli /usr/local/bin/docker /usr/local/bin/docker
RUN mkdir -p /data && chown $APP_UID /data
WORKDIR /app
COPY --from=build /app ./
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "TeachingAssistant.WebAPI.dll"]
