FROM node:22-alpine AS frontend-build
WORKDIR /frontend
COPY frontend/savranpay-web/package*.json ./
RUN npm ci
COPY frontend/savranpay-web ./
ARG VITE_API_BASE_URL=
ARG VITE_DEMO_TRANSFER_SECRET=
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
ENV VITE_DEMO_TRANSFER_SECRET=$VITE_DEMO_TRANSFER_SECRET
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/SavranPay.Api/SavranPay.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /app/publish .
COPY --from=frontend-build /frontend/dist ./wwwroot
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "exec dotnet SavranPay.Api.dll --urls \"http://0.0.0.0:${PORT:-${ASPNETCORE_HTTP_PORTS:-8080}}\""]
