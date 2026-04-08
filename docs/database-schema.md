# Esquema do Banco de Dados — VintageCodeAcademy

PostgreSQL via Supabase. Todas as tabelas usam `uuid` como chave primária.

## Tabelas

### users
| Coluna         | Tipo        | Descrição                        |
|----------------|-------------|----------------------------------|
| id             | uuid PK     | ID do Supabase Auth              |
| email          | varchar(255)| Único                            |
| name           | varchar(100)|                                  |
| avatar_url     | varchar(500)| Opcional                         |
| xp             | int         | Default: 0                       |
| level          | int         | Enum UserLevel (0–5)             |
| streak_days    | int         | Default: 0                       |
| created_at     | timestamptz |                                  |
| last_activity_at | timestamptz | Nullable                      |

### trails
| Coluna       | Tipo         | Descrição                        |
|--------------|--------------|----------------------------------|
| id           | uuid PK      |                                  |
| title        | varchar(200) |                                  |
| description  | text         |                                  |
| stack        | varchar(100) | Ex: "C#", "React", "Python"      |
| level        | int          | Enum TrailLevel (0=Beginner)     |
| order        | int          | Ordem de exibição                |
| is_published | bool         | Default: false                   |
| created_at   | timestamptz  |                                  |

### modules
| Coluna    | Tipo        | Descrição           |
|-----------|-------------|---------------------|
| id        | uuid PK     |                     |
| trail_id  | uuid FK     | → trails.id         |
| title     | varchar(200)|                     |
| order     | int         |                     |
| created_at| timestamptz |                     |

### lessons
| Coluna       | Tipo        | Descrição                          |
|--------------|-------------|------------------------------------|
| id           | uuid PK     |                                    |
| module_id    | uuid FK     | → modules.id                       |
| title        | varchar(200)|                                    |
| content_json | jsonb       | Estrutura gamificada gerada pela IA|
| xp_reward    | int         | Default: 10                        |
| order        | int         |                                    |
| status       | int         | Enum LessonStatus (0=Draft)        |
| created_at   | timestamptz |                                    |
| published_at | timestamptz | Nullable                           |

### lesson_chunks
| Coluna       | Tipo        | Descrição                    |
|--------------|-------------|------------------------------|
| id           | uuid PK     |                              |
| lesson_id    | uuid FK     | → lessons.id                 |
| chunk_index  | int         | Ordem do fragmento no PDF    |
| raw_text     | text        | Texto extraído pelo PdfPig   |
| generated_at | timestamptz |                              |

### quizzes
| Coluna        | Tipo         | Descrição                  |
|---------------|--------------|----------------------------|
| id            | uuid PK      |                            |
| lesson_id     | uuid FK      | → lessons.id               |
| question      | varchar(1000)|                            |
| options_json  | jsonb        | Array de strings           |
| correct_index | int          | Índice da resposta correta |
| explanation   | varchar(2000)|                            |
| created_at    | timestamptz  |                            |

### quiz_attempts
| Coluna       | Tipo        | Descrição                    |
|--------------|-------------|------------------------------|
| id           | uuid PK     |                              |
| user_id      | uuid FK     | → users.id                   |
| lesson_id    | uuid FK     | → lessons.id                 |
| score        | int         | Número de acertos            |
| answers_json | jsonb       | Array de índices escolhidos  |
| attempted_at | timestamptz |                              |

### user_progress
| Coluna      | Tipo        | Descrição    |
|-------------|-------------|--------------|
| id          | uuid PK     |              |
| user_id     | uuid FK     | → users.id   |
| lesson_id   | uuid FK     | → lessons.id |
| completed_at| timestamptz |              |
| xp_earned   | int         |              |

### badges
| Coluna      | Tipo         | Descrição              |
|-------------|--------------|------------------------|
| id          | uuid PK      |                        |
| code        | varchar(50)  | Único                  |
| name        | varchar(100) |                        |
| description | text         |                        |
| icon_url    | varchar(500) | Nullable               |
| xp_bonus    | int          |                        |

### user_badges
| Coluna    | Tipo        | Descrição   |
|-----------|-------------|-------------|
| id        | uuid PK     |             |
| user_id   | uuid FK     | → users.id  |
| badge_id  | uuid FK     | → badges.id |
| earned_at | timestamptz |             |

### rankings
| Coluna    | Tipo        | Descrição              |
|-----------|-------------|------------------------|
| id        | uuid PK     |                        |
| user_id   | uuid FK     | → users.id             |
| week      | int         | Número ISO da semana   |
| xp_earned | int         | XP ganho na semana     |
| position  | int         | Posição no ranking     |

### ai_generation_logs
| Coluna            | Tipo         | Descrição               |
|-------------------|--------------|-------------------------|
| id                | uuid PK      |                         |
| lesson_id         | uuid FK      | → lessons.id            |
| model             | varchar(50)  | Ex: "deepseek-chat"     |
| prompt_tokens     | int          |                         |
| completion_tokens | int          |                         |
| cost_usd          | decimal(10,6)|                         |
| created_at        | timestamptz  |                         |

### donations
| Coluna             | Tipo          | Descrição                     |
|--------------------|---------------|-------------------------------|
| id                 | uuid PK       |                               |
| user_id            | uuid FK       | → users.id (nullable)         |
| amount             | decimal(10,2) |                               |
| provider           | int           | Enum (0=Stripe, 1=MercadoPago)|
| status             | int           | Enum DonationStatus           |
| external_reference | varchar(200)  | ID do provedor externo        |
| created_at         | timestamptz   |                               |
