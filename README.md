**ASP.NET Core 8**, **Clean Architecture**, **CQRS + MediatR**, **PostgreSQL**, **Redis**, **SignalR**.

- .NET 8 SDK
- Docker + Docker Compose
- HuggingFace API Key (бесплатно на huggingface.co)

API Endpoints

### Auth
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/v1/auth/register` | Регистрация нового пользователя |
| POST | `/api/v1/auth/login` | Авторизация (возвращает JWT) |
| GET | `/api/v1/auth/me` | Профиль текущего пользователя |

### Analyses
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/v1/analyses` | Создать анализ (ручной ввод / GitHub) |
| POST | `/api/v1/analyses/upload` | Загрузить файл для анализа |
| GET | `/api/v1/analyses` | Список анализов (постраничный) |
| GET | `/api/v1/analyses/{id}` | Детали анализа с issues и метриками |
| DELETE | `/api/v1/analyses/{id}` | Удалить анализ |

### GitHub
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/v1/github/connect` | Подключить GitHub (OAuth code) |
| DELETE | `/api/v1/github/disconnect` | Отключить GitHub |
| GET | `/api/v1/github/repositories` | Список репозиториев |
| GET | `/api/v1/github/repositories/{owner}/{repo}/pull-requests` | PR-ы репозитория |
| POST | `/api/v1/github/repositories/{owner}/{repo}/pull-requests/{pr}/analyze` | Анализ PR |

### SignalR Hub
```
ws://localhost:5000/hubs/analysis?access_token=<JWT>
```
События: `AnalysisStarted`, `AnalysisProgress`, `AnalysisCompleted`, `AnalysisFailed`

### Health
- `/health` — полный статус
- `/health/ready` — инфраструктура (PostgreSQL, Redis)
- `/health/live` — liveness probe

Используется **HuggingFace Inference API** с моделью `moonshotai/Kimi-K2-Instruct`.
Архитектура провайдеров расширяема через `OpenAICompatibleProvider`

Pipeline анализа

```
POST /api/v1/analyses
    → CreateAnalysisCommand
    → ValidationBehavior (FluentValidation)
    → CreateAnalysisCommandHandler
        1. Создаёт CodeAnalysis агрегат (Pending)
        2. Сохраняет в БД
        3. Запускает ProcessAnalysisAsync (fire-and-forget)
           ├── StaticCodeAnalyzer → цикломатическая сложность, дублирование
           ├── HuggingFaceProvider → SOLID violations, anti-patterns, score
           ├── CodeAnalysis.Complete() → сохраняет results
           └── SignalR → уведомляет клиента в реальном времени
    → Возвращает 201 Created с AnalysisSummaryDto
```

