FROM mcr.microsoft.com/dotnet/core/sdk:2.2 AS build
WORKDIR /app

# copy csproj and restore as distinct layers
COPY *.sln .
COPY ZES.GraphQL.AspNetCore ./ZES.GraphQL.AspNetCore
COPY ZES ./ZES
COPY ZES.Interfaces ./ZES.Interfaces
COPY ZES.Infrastructure ./ZES.Infrastructure
COPY ZES.GraphQL ./ZES.GraphQL
COPY ZES.Tests ./ZES.Tests
COPY ZES.Tests.Domain ./ZES.Tests.Domain
RUN dotnet restore 

# copy everything else and build app
WORKDIR /app/ZES.GraphQL.AspNetCore
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/core/aspnet:2.2 AS runtime
WORKDIR /app
COPY --from=build /app/ZES.GraphQL.AspNetCore/out ./
ENTRYPOINT ["dotnet", "ZES.GraphQL.AspNetCore.dll"]