# Arquitetura do VintageCodeAcademy

## Visão Geral

O VintageCodeAcademy segue **Clean Architecture** com separação clara de responsabilidades em quatro camadas:

```
Domain → Application → Infrastructure → API / Web
```

Dependências apontam sempre de fora para dentro. A camada Domain não depende de nada.

---

## Camadas

### VCA.Domain
- Entidades de negócio com comportamento encapsulado (sem setters públicos)
- Interfaces de repositório e Unit of Work
- Enums do domínio
- `Result<T>` pattern para retorno sem exceções

### VCA.Application
- Use cases organizados por feature (`/AI`, `/Gamification`, `/Courses`, `/Users`)
- Cada feature tem `Command/Query + Handler + Result`
- Interfaces de serviços externos (sem implementações)
- `DependencyInjection.cs` registra os handlers

### VCA.Infrastructure
- Implementações EF Core com Npgsql (PostgreSQL via Supabase)
- `AppDbContext` + configurações Fluent API por entidade
- `BaseRepository<T>` + repositórios específicos
- `UnitOfWork` centraliza todos os repositórios e `SaveChanges`
- Serviços externos: DeepSeek, Resend, Supabase Storage, PdfPig

### VCA.API
- ASP.NET Core Web API com JWT Bearer (Supabase Auth)
- `ErrorHandlingMiddleware` — tratamento global de exceções
- `UserContextMiddleware` — extrai `UserId` do JWT
- `RankingHub` — SignalR para ranking em tempo real
- Swagger com segurança JWT configurada

### VCA.Web
- Blazor WebAssembly + MudBlazor
- Serviços que consomem a VCA API via `HttpClient`
- SignalR client para ranking ao vivo

---

## Fluxo VCA Intelligence (Geração por IA)

```
1. Admin faz upload do PDF → POST /api/ai/lessons/{id}/generate-from-pdf
2. PdfExtractorService (PdfPig) extrai texto e divide em chunks de 1500 chars
3. Chunks são persistidos em lesson_chunks
4. DeepSeekService chama API DeepSeek V3 com prompt estruturado
5. JSON gamificado retornado é salvo em lessons.content_json
6. Status da aula muda para PendingReview
7. Admin revisa e publica → lição disponível para alunos
8. AiGenerationLog registra tokens usados e custo em USD
```

---

## Sistema de Gamificação

| Ação                  | XP      |
|-----------------------|---------|
| Completar aula        | +10     |
| Completar desafio     | +15     |
| Completar quiz        | +30     |
| Streak 7 dias         | +100    |
| Projeto Labs          | +200    |
| Top 3 semanal         | +300/500|

### Níveis de Progressão
| Nível       | XP Necessário |
|-------------|---------------|
| Rookie      | 0             |
| Apprentice  | 500           |
| Builder     | 1.500         |
| Craftsman   | 4.000         |
| Expert      | 10.000        |
| Vintage Dev | 25.000        |

---

## Banco de Dados

PostgreSQL via Supabase. EF Core com Migrations versionadas.

Todas as entidades usam `Guid` como chave primária.
Datas sempre em UTC.
`ContentJson` e `OptionsJson` usam tipo `jsonb` no PostgreSQL.

---

## Autenticação

1. Usuário se autentica via Supabase Auth (email/senha, Google OAuth, GitHub OAuth)
2. Supabase retorna JWT
3. Frontend inclui JWT no header: `Authorization: Bearer {token}`
4. API valida o JWT com o `JwtSecret` do Supabase
5. `UserContextMiddleware` extrai o `sub` claim e disponibiliza como `UserId`
6. Primeiro login chama `POST /api/users/sync` para sincronizar o usuário no banco da aplicação

---

## Deploy

| Serviço     | Plataforma |
|-------------|------------|
| Blazor WASM | Vercel     |
| API         | Railway    |
| Banco       | Supabase   |
| Storage     | Supabase Storage (PDFs/avatares) + Bunny.net (vídeos) |
| Monitor     | Sentry     |
| Analytics   | Umami      |
