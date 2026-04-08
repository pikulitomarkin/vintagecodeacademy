# Sistema de Gamificação — VintageCodeAcademy

## XP e Níveis

O progresso do usuário é medido em XP (Experience Points). A entidade `User` calcula o nível automaticamente ao adicionar XP.

### Tabela de Níveis
```
Rookie      →  0 XP (início)
Apprentice  →  500 XP
Builder     →  1.500 XP
Craftsman   →  4.000 XP
Expert      →  10.000 XP
Vintage Dev →  25.000 XP
```

## Ações que Concedem XP

| Ação                        | XP  | Implementado em            |
|-----------------------------|-----|---------------------------|
| Completar aula              | +10 | `CompleteLessonHandler`   |
| Completar desafio (lab)     | +15 | (a implementar)           |
| Completar quiz (≥60%)       | +30 | `SubmitQuizHandler`       |
| Streak 7 dias consecutivos  | +100| (a implementar via cron)  |
| Projeto Labs concluído      | +200| (a implementar)           |
| Top 1 ranking semanal       | +300| (a implementar via cron)  |
| Top 2 ranking semanal       | +200| (a implementar via cron)  |
| Top 3 ranking semanal       | +100| (a implementar via cron)  |

## Quiz

- Cada módulo tem um pool de **10 questões**
- O aluno recebe **5 questões** selecionadas por seed determinístico `(userId + lessonId)`
- Máximo de **2 tentativas** por aula
- Mínimo de **60% de acerto** para ganhar o XP do quiz

## Badges

Badges são conquistas registradas em `user_badges`. Concedem XP bônus.
Exemplos de badges a implementar:

| Badge Code          | Nome               | XP Bônus |
|---------------------|--------------------|----------|
| `first_lesson`      | Primeiro Passo     | 50       |
| `streak_7`          | Semana Dedicada    | 100      |
| `quiz_perfect`      | Quiz Perfeito      | 75       |
| `trail_completed`   | Trilha Concluída   | 200      |
| `top3_weekly`       | Pódio Semanal      | 150      |

## Ranking Semanal

- Ranking calculado com base no XP ganho na semana corrente (ISO week number)
- Atualizado em tempo real via **SignalR** (`RankingHub`)
- Bônus de XP distribuídos ao final de cada semana por job agendado

## Streak

- `StreakDays` incrementa a cada dia de atividade consecutiva
- Resetado automaticamente se o usuário não interagir em 24h
- Bônus de +100 XP ao completar 7 dias de streak
