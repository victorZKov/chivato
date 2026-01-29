# Diseño Visual - Chivato

## Identidad Visual

- **Colores primarios**: Negro / Naranja
- **Temas**: Light (día), Dark (noche), System (auto)
- **Tipografía**: Inter

## Paleta de Colores

### Tema Light (Día)

```css
:root[data-theme="light"] {
  /* Backgrounds */
  --bg-primary: #FFFFFF;
  --bg-secondary: #F5F5F5;
  --bg-tertiary: #EBEBEB;

  /* Text */
  --text-primary: #1A1A1A;
  --text-secondary: #4A4A4A;
  --text-muted: #7A7A7A;

  /* Brand - Naranja */
  --brand-primary: #FF6B00;
  --brand-hover: #E55F00;
  --brand-active: #CC5500;
  --brand-light: #FFF0E6;

  /* Negro/Dark accents */
  --accent-dark: #1A1A1A;
  --accent-dark-hover: #2A2A2A;

  /* Borders */
  --border-default: #E0E0E0;
  --border-strong: #C0C0C0;

  /* Semantic - Estados */
  --success: #22C55E;
  --success-bg: #F0FDF4;
  --warning: #F59E0B;
  --warning-bg: #FFFBEB;
  --error: #EF4444;
  --error-bg: #FEF2F2;
  --info: #3B82F6;
  --info-bg: #EFF6FF;

  /* Drift Severity */
  --severity-critical: #DC2626;
  --severity-high: #EA580C;
  --severity-medium: #F59E0B;
  --severity-low: #3B82F6;
  --severity-info: #6B7280;
}
```

### Tema Dark (Noche)

```css
:root[data-theme="dark"] {
  /* Backgrounds */
  --bg-primary: #0A0A0A;
  --bg-secondary: #141414;
  --bg-tertiary: #1F1F1F;

  /* Text */
  --text-primary: #FAFAFA;
  --text-secondary: #A3A3A3;
  --text-muted: #737373;

  /* Brand - Naranja */
  --brand-primary: #FF7A1A;
  --brand-hover: #FF8C33;
  --brand-active: #FF6B00;
  --brand-light: #1A1208;

  /* Negro/Dark accents (inverted for dark mode) */
  --accent-dark: #FAFAFA;
  --accent-dark-hover: #E5E5E5;

  /* Borders */
  --border-default: #2A2A2A;
  --border-strong: #3A3A3A;

  /* Semantic - Estados */
  --success: #4ADE80;
  --success-bg: #052E16;
  --warning: #FBBF24;
  --warning-bg: #1C1A05;
  --error: #F87171;
  --error-bg: #1C0505;
  --info: #60A5FA;
  --info-bg: #05101C;

  /* Drift Severity */
  --severity-critical: #F87171;
  --severity-high: #FB923C;
  --severity-medium: #FBBF24;
  --severity-low: #60A5FA;
  --severity-info: #9CA3AF;
}
```

## Tipografía

### Fuente: Inter

```css
@import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap');

:root {
  --font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;

  /* Font sizes */
  --text-xs: 0.75rem;    /* 12px */
  --text-sm: 0.875rem;   /* 14px */
  --text-base: 1rem;     /* 16px */
  --text-lg: 1.125rem;   /* 18px */
  --text-xl: 1.25rem;    /* 20px */
  --text-2xl: 1.5rem;    /* 24px */
  --text-3xl: 1.875rem;  /* 30px */
  --text-4xl: 2.25rem;   /* 36px */

  /* Font weights */
  --font-normal: 400;
  --font-medium: 500;
  --font-semibold: 600;
  --font-bold: 700;

  /* Line heights */
  --leading-tight: 1.25;
  --leading-normal: 1.5;
  --leading-relaxed: 1.75;
}
```

### Jerarquía Tipográfica

| Elemento | Size | Weight | Uso |
|----------|------|--------|-----|
| H1 | 2.25rem (36px) | Bold | Títulos de página |
| H2 | 1.5rem (24px) | Semibold | Secciones principales |
| H3 | 1.25rem (20px) | Semibold | Subsecciones |
| H4 | 1.125rem (18px) | Medium | Cards, panels |
| Body | 1rem (16px) | Normal | Texto general |
| Small | 0.875rem (14px) | Normal | Labels, ayuda |
| Caption | 0.75rem (12px) | Normal | Metadata, timestamps |

## Componentes Clave

### Botones

```css
/* Primary Button - Naranja */
.btn-primary {
  background: var(--brand-primary);
  color: white;
  font-weight: var(--font-medium);
  padding: 0.5rem 1rem;
  border-radius: 0.375rem;
  transition: background 0.15s;
}
.btn-primary:hover {
  background: var(--brand-hover);
}

/* Secondary Button - Negro */
.btn-secondary {
  background: var(--accent-dark);
  color: white;
  /* dark mode inverts this */
}

/* Outline Button */
.btn-outline {
  background: transparent;
  border: 1px solid var(--brand-primary);
  color: var(--brand-primary);
}
```

### Cards

```css
.card {
  background: var(--bg-secondary);
  border: 1px solid var(--border-default);
  border-radius: 0.5rem;
  padding: 1.5rem;
}

.card-elevated {
  box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1);
}
```

### Badges de Severidad

```css
.badge-critical {
  background: var(--severity-critical);
  color: white;
}
.badge-high {
  background: var(--severity-high);
  color: white;
}
.badge-medium {
  background: var(--severity-medium);
  color: black;
}
.badge-low {
  background: var(--severity-low);
  color: white;
}
.badge-info {
  background: var(--severity-info);
  color: white;
}
```

### Estado de Credenciales

```css
/* Indicadores de expiracion */
.credential-status-ok {      /* > 30 dias */
  color: var(--success);
}
.credential-status-warning { /* 7-30 dias */
  color: var(--warning);
}
.credential-status-danger {  /* < 7 dias */
  color: var(--error);
}
```

## Layout Principal

```
┌─────────────────────────────────────────────────────────────────────────┐
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │  HEADER                                               [User] ▼  │   │
│  │  🔥 Chivato          [Dashboard] [Pipelines] [Config]  🌙/☀️   │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                                                                         │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                                                                  │   │
│  │   MAIN CONTENT AREA                                             │   │
│  │                                                                  │   │
│  │   ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │   │
│  │   │ Metric Card  │  │ Metric Card  │  │ Metric Card  │          │   │
│  │   │   🔴 12      │  │   🟡 45      │  │   🟢 120     │          │   │
│  │   │   Critical   │  │   Warning    │  │   OK         │          │   │
│  │   └──────────────┘  └──────────────┘  └──────────────┘          │   │
│  │                                                                  │   │
│  │   ┌─────────────────────────────────────────────────────────┐   │   │
│  │   │  Recent Drift                                     [Filter]│   │   │
│  │   ├─────────────────────────────────────────────────────────┤   │   │
│  │   │  🔴 CRITICAL  App Service SKU changed     Pipeline-1    │   │   │
│  │   │  🟠 HIGH      Storage redundancy drift    Pipeline-2    │   │   │
│  │   │  🟡 MEDIUM    Tags missing                Pipeline-1    │   │   │
│  │   └─────────────────────────────────────────────────────────┘   │   │
│  │                                                                  │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                                                                         │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │  FOOTER                                          v1.0.0 | Docs  │   │
│  └─────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────┘
```

## Theme Switcher

```typescript
// Implementacion del theme switcher
type Theme = 'light' | 'dark' | 'system';

function useTheme() {
  const [theme, setTheme] = useState<Theme>('system');

  useEffect(() => {
    const root = document.documentElement;

    if (theme === 'system') {
      const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
      root.setAttribute('data-theme', prefersDark ? 'dark' : 'light');
    } else {
      root.setAttribute('data-theme', theme);
    }
  }, [theme]);

  return { theme, setTheme };
}
```

### Iconos del Theme Switcher

| Theme | Icono | Label |
|-------|-------|-------|
| Light | ☀️ / `<SunIcon />` | Día |
| Dark | 🌙 / `<MoonIcon />` | Noche |
| System | 💻 / `<ComputerIcon />` | Sistema |

## Logo

El logo "Chivato" usa:
- **Icono**: 🔥 (fuego/alerta) o icono personalizado
- **Texto**: "Chivato" en Inter Bold
- **Color**: Naranja (`--brand-primary`) sobre fondo oscuro/claro

```
Light mode:  🔥 Chivato  (naranja sobre blanco)
Dark mode:   🔥 Chivato  (naranja sobre negro)
```

## Spacing System

```css
:root {
  --space-1: 0.25rem;  /* 4px */
  --space-2: 0.5rem;   /* 8px */
  --space-3: 0.75rem;  /* 12px */
  --space-4: 1rem;     /* 16px */
  --space-5: 1.25rem;  /* 20px */
  --space-6: 1.5rem;   /* 24px */
  --space-8: 2rem;     /* 32px */
  --space-10: 2.5rem;  /* 40px */
  --space-12: 3rem;    /* 48px */
  --space-16: 4rem;    /* 64px */
}
```

## Border Radius

```css
:root {
  --radius-sm: 0.25rem;  /* 4px - badges, small elements */
  --radius-md: 0.375rem; /* 6px - buttons, inputs */
  --radius-lg: 0.5rem;   /* 8px - cards, panels */
  --radius-xl: 0.75rem;  /* 12px - modals */
  --radius-full: 9999px; /* pills, avatars */
}
```

## Animaciones

```css
:root {
  --transition-fast: 0.1s ease;
  --transition-normal: 0.15s ease;
  --transition-slow: 0.3s ease;
}

/* Hover effects */
.interactive {
  transition: all var(--transition-normal);
}

/* Loading spinner - usa color naranja */
.spinner {
  border-color: var(--brand-primary);
}
```

## Responsive Breakpoints

```css
/* Tailwind-style breakpoints */
--breakpoint-sm: 640px;
--breakpoint-md: 768px;
--breakpoint-lg: 1024px;
--breakpoint-xl: 1280px;
--breakpoint-2xl: 1536px;
```
