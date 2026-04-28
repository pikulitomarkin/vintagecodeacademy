# VintageCodeAcademy — Contexto Global da Equipe Multi-IA

## Sobre o Projeto
VintageCodeAcademy é uma plataforma educacional gratuita desenvolvida pela 
Vintage DevStack. Ensino gamificado de programação com IA generativa para 
geração de conteúdo, quiz dinâmico e aulas estruturadas como quests.

## Responsável Técnico
Marcos Roberto Padilha — CEO Vintage DevStack
Papel: QA final, DevOps, aprovação de PRs e decisões de arquitetura.

## Stack Definitiva
- Frontend:  Blazor WebAssembly (.NET 8, SSR híbrido) + MudBlazor
- Backend:   ASP.NET Core Web API (.NET 8)
- Banco:     PostgreSQL via Supabase (Npgsql + EF Core)
- Auth:      Supabase Auth (JWT + OAuth Google/GitHub)
- Storage:   Supabase Storage (PDFs, avatares) + Bunny.net (vídeos)
- IA/LLM:    DeepSeek API (V3) — geração de aulas e quizzes
- PDF:       PdfPig (.NET) — extração e chunking
- Realtime:  SignalR (.NET nativo) — ranking ao vivo
- Email:     Resend — transacional
- Deploy:    Vercel (Blazor WASM) + Railway (API) + Supabase (cloud)
- Doações:   Stripe + Mercado Pago (Pix)
- Monitor:   Sentry + Umami Analytics

## Estrutura do Repositório
vintagecodea cademy/
├── src/
│   ├── VCA.API/                  # ASP.NET Core Web API
│   │   ├── Controllers/
│   │   ├── Middleware/
│   │   └── Program.cs
│   ├── VCA.Application/          # Use cases / Services
│   │   ├── AI/
│   │   ├── Gamification/
│   │   ├── Courses/
│   │   └── Users/
│   ├── VCA.Domain/               # Entidades e interfaces
│   │   ├── Entities/
│   │   ├── Interfaces/
│   │   └── Enums/
│   ├── VCA.Infrastructure/       # EF Core, Supabase, DeepSeek
│   │   ├── Data/
│   │   ├── Repositories/
│   │   └── ExternalServices/
│   └── VCA.Web/                  # Blazor WASM
│       ├── Pages/
│       ├── Components/
│       ├── Services/
│       └── Layout/
├── tests/
│   ├── VCA.UnitTests/
│   └── VCA.IntegrationTests/
└── docs/

## Entidades do Banco (PostgreSQL)
- users              → id, email, name, avatar_url, xp, level, streak_days, created_at
- trails             → id, title, description, stack, level, order, is_published
- modules            → id, trail_id, title, order
- lessons            → id, module_id, title, content_json, xp_reward, order, status
- lesson_chunks      → id, lesson_id, chunk_index, raw_text, generated_at
- quizzes            → id, lesson_id, question, options_json, correct_index, explanation
- quiz_attempts      → id, user_id, lesson_id, score, answers_json, attempted_at
- user_progress      → id, user_id, lesson_id, completed_at, xp_earned
- badges             → id, code, name, description, icon_url, xp_bonus
- user_badges        → id, user_id, badge_id, earned_at
- rankings           → id, user_id, week, xp_earned, position
- labs_projects      → id, title, description, stack, status, slots_available
- labs_applications  → id, user_id, project_id, status, applied_at
- donations          → id, user_id, amount, provider, status, created_at
- ai_generation_logs → id, lesson_id, model, prompt_tokens, completion_tokens, cost_usd, created_at

## Sistema de Gamificação
Níveis: Rookie(0) → Apprentice(500) → Builder(1.5k) → Craftsman(4k) → Expert(10k) → Vintage Dev(25k)
XP: aula+10, desafio+15, quiz+30, streak7d+100, projeto+200, top3semanal+300/500

## Módulo VCA Intelligence (IA)
Fluxo: PDF upload → PdfPig chunking → DeepSeek API → JSON gamificado → revisão admin → publicação
Estrutura de aula: Missão | Contexto Real | Conceito | Desafio Rápido | Exemplo | Quiz | Resumo + XP
Quiz: pool 10 questões/módulo, aluno recebe 5 (seed = user_id + lesson_id), 2 tentativas máx

## Regras Gerais para Toda a Equipe
- Idioma do código: inglês (variáveis, métodos, classes)
- Comentários e docs: português brasileiro
- Padrão: Clean Architecture (Domain → Application → Infrastructure → API)
- ORM: EF Core com Migrations versionadas
- Validação: FluentValidation em todos os commands/requests
- Erros: Result Pattern (sem throw desnecessário)
- Autenticação: JWT Bearer, middleware de extração de userId
- Nunca hardcodar secrets — sempre usar IConfiguration/env vars
- Testes: xUnit + Moq para unit, TestContainers para integration
- Commits: Conventional Commits (feat:, fix:, chore:, docs:)