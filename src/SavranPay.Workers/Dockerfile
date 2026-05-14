FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/SavranPay.Workers/SavranPay.Workers.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SavranPay.Workers.dll"]
