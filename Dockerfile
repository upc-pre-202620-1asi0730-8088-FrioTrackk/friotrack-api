FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY FrioTrack.Api.csproj ./
RUN dotnet restore FrioTrack.Api.csproj
COPY . ./
RUN dotnet publish FrioTrack.Api.csproj -c Release -o /app/publish --no-restore --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "FrioTrack.Api.dll"]
