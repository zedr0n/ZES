FROM mcr.microsoft.com/dotnet/core/aspnet:2.2 AS runtime
WORKDIR /app
COPY bin/Release/netcoreapp2.2/publish /app
ENTRYPOINT ["dotnet", "ZES.GraphQL.AspNetCore.dll"]