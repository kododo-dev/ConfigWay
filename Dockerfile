FROM node:22-alpine AS spa
WORKDIR /spa
COPY src/ConfigWay.UI/SPA/package*.json ./
RUN npm ci
COPY src/ConfigWay.UI/SPA/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/ ./
COPY --from=spa /spa/dist ./ConfigWay.UI/SPA/dist
RUN dotnet publish ConfigWay.Demo.Web/ConfigWay.Demo.Web.csproj \
    --configuration Release \
    --framework net10.0 \
    --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Kododo.ConfigWay.Demo.Web.dll"]
