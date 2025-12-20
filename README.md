# Gigs (API)

This project serves as the backend for the Gigs website, built with .NET Core and utilizing a Postgres database for data persistence. It's designed to provide a robust and scalable API, ensuring secure and efficient access to data for the frontend application.

## Live Demo

Visit [api.gigs.josephpickering.co.uk](https://api.experience.josephpickering.co.uk) to see the project in action.

## Table of Contents

- [Overview](#overview)
    - [Features](#features)
- [Getting Started](#getting-started)
    - [Prerequisites](#prerequisites)
    - [Installation](#installation)
- [Deployment](#deployment)

## Overview

The Gigs API is a .NET Core application that provides a RESTful API for the Gigs website. It's designed to be a scalable and reliable backend, providing access to data for the frontend application. The API is built with a focus on performance, security, and reliability, ensuring that it can handle the demands of a production environment.

### Features

- **.NET Core Framework:** Utilizes the latest .NET Core technologies for a high-performance, cross-platform API.
- **Postgres Database:** Leverages Postgres for reliable and scalable data storage.
- **Comprehensive Testing:** Includes both unit and integration tests to ensure code reliability and quality.
- **CI/CD:** Utilizes GitHub Actions for continuous integration and deployment, automatically running tests on pull requests to prevent merging test failures.
- **Deployment:** Automated deployment to a Digital Ocean droplet ensures seamless updates and availability.
- **Authentication:** Integrated with Auth0 for secure and scalable user authentication.

## Getting Started

### Prerequisites

- .NET Core SDK (version specified in `global.json`)
- PostgreSQL installed and running
- Auth0 account for setting up authentication

### Installation

1. **Clone the repository:**

   ```bash
   git clone https://github.com/josephpickering9/gigs_api.git
   cd gigs_api
    ```
   
2. **Install dependencies:**

    ```bash
    dotnet restore
    ```

3. **Auth0 Configuration**:

    Set up your Auth0 application and API configurations. Provide the necessary settings in appsettings.json or as environment variables for authentication to work.

4. **Set up environment variables:**

    Create a `.env` file (you can use .env.example as reference) in the root of the project and add the following environment variables:

    ```env
    DATABASE_URL=postgres://username:password@localhost:5432/gigs
    AUTH0_DOMAIN=your-auth0-domain
    AUTH0_AUDIENCE=your-auth0-audience
    ```
   
5. **Run the application:**

    ```bash
    dotnet run
    ```
   
    This will start the API on `http://localhost:5105` by default.

6. **Run tests:**

    ```bash
    dotnet test
    ```
   
## Deployment
This project is configured for CI/CD with GitHub Actions, automatically deploying to a Digital Ocean droplet upon successful pull request merges. Ensure your Digital Ocean and GitHub repository settings are configured correctly for deployments.
