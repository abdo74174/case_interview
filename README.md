# CaseInterview Backend (.NET 8 Clean Architecture)

Backend API for **CaseInterview** — a system design mock interview platform that extracts chapters from uploaded engineering books, embeds text chunks with vector embeddings (pgvector), and provides realistic case-based interviews.

---

## 🏗️ Architecture

Built following **Clean Architecture** principles with 4 decoupled layers:

- **`CaseInterview.Domain`**: Core entities (`Book`, `Chapter`, `ChunkEmbedding`, `InterviewCase`, `InterviewSession`, `SessionMessage`, `SessionFeedback`), value objects, domain enums, and domain exceptions.
- **`CaseInterview.Application`**: Use cases, CQRS/Service interfaces, DTOs, FluentValidation validators.
- **`CaseInterview.Infrastructure`**: EF Core persistence (PostgreSQL with `pgvector` & InMemory fallback), BCrypt password hashing, JWT token generator, PdfPig chapter extraction, Semantic text chunker (300-500 tokens, 50-token overlap), Microsoft.Extensions.AI embedding integration, and background processing (`System.Threading.Channels.Channel<T>`).
- **`CaseInterview.Api`**: Controllers (`AuthController`, `BooksController`, `ChaptersController`), Serilog structured logging, Swagger documentation with JWT Bearer support, and RFC 7807 `ProblemDetails` global exception handling.
- **`CaseInterview.UnitTests`**: Comprehensive unit and integration test suite with 15 passing tests.

---

## 🚀 Quick Start

### 1. Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker](https://www.docker.com/) (Optional: only needed if using PostgreSQL + pgvector)

### 2. Start PostgreSQL with pgvector (Optional)
```bash
docker compose up -d
```
*Note: If Docker is not running, the API automatically falls back to the in-memory provider configured in `appsettings.json` (`"UseInMemoryDatabase": true`).*

### 3. Run Database Migrations (PostgreSQL)
```bash
dotnet ef database update --project src/CaseInterview.Infrastructure --startup-project src/CaseInterview.Api
```

### 4. Run the API
From the `backend` folder:
```bash
dotnet run --project src/CaseInterview.Api/CaseInterview.Api.csproj --urls "http://localhost:5050"
```
Or in Visual Studio:
- Set `CaseInterview.Api` as the **Startup Project** and press <kbd>F5</kbd>.

### 5. Access Endpoints
- **Swagger UI**: [http://localhost:5050/swagger](http://localhost:5050/swagger)
- **Base API**: `http://localhost:5050/api`

---

## 🧪 Running Unit & Integration Tests
```bash
dotnet test tests/CaseInterview.UnitTests/CaseInterview.UnitTests.csproj
```

---

## 🔑 Default Credentials
- **Email**: `architect@caseinterview.io`
- **Password**: `Password123!`
