FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG VERTEX_AI_PROJECT_ID
ARG VERTEX_AI_LOCATION
ARG VERTEX_AI_COLLECTION
ARG VERTEX_AI_BRANCH
ARG VERTEX_AI_ENVIRONMENT
ARG VERTEX_AI_MODEL
ARG VERTEX_AI_MODEL_LOCATION
ARG VERTEX_AI_QUERY_DATASTORE_SUFFIX
WORKDIR /src
COPY ["Gigs/Gigs.csproj", "Gigs/"]
RUN dotnet restore "Gigs/Gigs.csproj"
COPY . .
WORKDIR "/src/Gigs"
RUN dotnet build "Gigs.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Gigs.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV VertexAi__ProjectId=${VERTEX_AI_PROJECT_ID} \
    VertexAi__Location=${VERTEX_AI_LOCATION} \
    VertexAi__Collection=${VERTEX_AI_COLLECTION} \
    VertexAi__Branch=${VERTEX_AI_BRANCH} \
    VertexAi__Environment=${VERTEX_AI_ENVIRONMENT} \
    VertexAi__Model=${VERTEX_AI_MODEL} \
    VertexAi__ModelLocation=${VERTEX_AI_MODEL_LOCATION} \
    VertexAi__QueryDataStoreSuffix=${VERTEX_AI_QUERY_DATASTORE_SUFFIX}
ENTRYPOINT ["dotnet", "Gigs.dll"]
