# Theme UI "LigCom" - identique a LigFin

LigCom (GescomSaas) reprend **exactement** le gabarit et les couleurs de LigFin (ComptaSaaS), pour que les deux applications de la suite Lig*** aient une identite visuelle commune.

## Apercu fidele au screenshot LigFin

```
┌─────────────────────┬───────────────────────────────────────────────────┐
│ [L] LigCom          │ Tableau de bord    [● En ligne] [🔍 Ctrl K] [🟢 Tenant] [👤]│
├─────────────────────┼───────────────────────────────────────────────────┤
│ 📊 Tableau de bord  │ Tableau de bord                  [+ Nouveau doc] │
│  ── ACTIF (BLEU) ── │                                                   │
│ COMMERCIAL          │ ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌────────┐│
│ 📄 Devis & Factures │ │ 🏦 Treso  │ │ 📊 Resul │ │ 📈 Prod  │ │ 📉 Char││
│ 🛒 Achats           │ │ -156M ↘   │ │ -141M ↘  │ │ 1.0G     │ │ 1.1G   ││
│ 📈 Operations       │ └──────────┘ └──────────┘ └──────────┘ └────────┘│
│ 👥 Tiers            │ ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌────────┐│
│                     │ │ Creances │ │ Dettes   │ │ Brouill. │ │ TVA    ││
│ CATALOGUE           │ │ 1.0G ✓   │ │ 1.1G ✘   │ │ 0 ✓      │ │ 0 ✓    ││
│ 📦 Articles         │ └──────────┘ └──────────┘ └──────────┘ └────────┘│
│ 🏷 Categories       │                                                   │
│ 💶 Listes prix      │ ┌────────────────────────────┐ ┌────────────────┐│
│ % Codes taxe        │ │ Evolution mensuelle        │ │ ⚠ A traiter    ││
│                     │ │   ▌█  ▌█                   │ │ ✓ Tout en ordre││
│ STOCK               │ │   bleu/saumon              │ │                ││
│ 📊 Etat du stock    │ └────────────────────────────┘ │ ⚠ Top charges  ││
│ 📦 Mouvements       │                                │ Aucune charge  ││
│ 🏬 Depots           │                                └────────────────┘│
│                     │                                                   │
│ FINANCE             │                                                   │
│ 💳 Encaissements    │                                                   │
│ 📅 Conditions paie. │                                                   │
│                     │                                                   │
│ ADMINISTRATION      │                                                   │
│ 👥 Utilisateurs     │                                                   │
│ ⚙ Parametres       │                                                   │
├─────────────────────┤                                                   │
│ [CC] charles@demo   │                                                   │
│ ⤴ Deconnexion       │                                                   │
└─────────────────────┴───────────────────────────────────────────────────┘
```

## Palette - identique a LigFin

### Brand bleu (= bouton "+ Nouvelle ecriture" + item actif sidebar)

| Token | Valeur | Usage |
|---|---|---|
| `--gc-brand` | `#2563eb` | Bouton primary, accent |
| `--gc-brand-hover` | `#1d4ed8` | Hover |
| `--gc-brand-100` | `#dbeafe` | Background item actif sidebar |
| `--gc-brand-500` | `#3b82f6` | Border-left item actif |

### Vert reserve aux pills (mode "En ligne" + tenant)

| Token | Valeur | Usage |
|---|---|---|
| `--gc-pill-green-bg` | `#dcfce7` | Background pill |
| `--gc-pill-green-fg` | `#15803d` | Texte pill |
| `--gc-pill-green-bd` | `#bbf7d0` | Border pill |

### Surfaces

| Token | Valeur | Usage |
|---|---|---|
| `--gc-bg` | `#f5f6f8` | Fond page |
| `--gc-surface` | `#ffffff` | Cards, topbar |
| `--gc-sidebar-bg` | `#fafbfc` | Sidebar |

## Fichiers livres

| Fichier | Role |
|---|---|
| [`wwwroot/css/gescomsaas-ui.css`](../src/GescomSaas.Web/wwwroot/css/gescomsaas-ui.css) | Feuille de style complete avec tokens LigFin |
| [`Pages/Shared/_LayoutCompta.cshtml`](../src/GescomSaas.Web/Pages/Shared/_LayoutCompta.cshtml) | Layout reproduisant le gabarit LigFin (sidebar claire, topbar avec pills, logo `[L] LigCom`) |
| [`Pages/DashboardCompta.cshtml`](../src/GescomSaas.Web/Pages/DashboardCompta.cshtml) | Page demo : 8 KPI tiles + chart Produits/Charges + panels "A traiter" / "Top charges" - reproduit le screenshot |

## Composants

### Sidebar claire (240px / 64px tablette / drawer mobile)

- Fond `#fafbfc`, border droite `#e5e7eb`
- Logo : carre sombre `[L]` + texte "LigCom" en haut (= "LigFin" sur le screenshot)
- Sections en MAJUSCULES tres petites : `COMMERCIAL`, `CATALOGUE`, `STOCK`, `FINANCE`, `ADMINISTRATION`, `PLATEFORME SAAS`
- Item actif : **fond bleu pale `#dbeafe` + texte bleu `#1d4ed8` + border-left bleu `#3b82f6`** (idem LigFin)
- Footer : avatar carre gradient gris + email + bouton "Deconnexion" rouge

### Topbar blanche (56px sticky)

- Breadcrumb / titre a gauche
- A droite, dans l'ordre : pill **vert** "En ligne" / Recherche `Ctrl+K` / pill **vert** "Tenant" / Avatar dropdown

### KPI tiles (`stat-card`)

```cshtml
<div class="stat-card">
    <div class="stat-card-icon stat-icon-blue"><i class="bi bi-graph-up"></i></div>
    <div class="stat-card-body">
        <div class="stat-card-label">Produits (exercice)</div>
        <div class="stat-card-value">1 012 377 269 FCFA</div>
    </div>
</div>
```

Variantes d'icones : `stat-icon-amber`, `rose`, `blue`, `green`, `emerald`, `sky`, `violet`, `slate`.

Couleurs des chiffres :
- `.stat-value-pos` (vert `#15803d`) pour les valeurs positives / produits
- `.stat-value-neg` (rouge `#dc2626`) pour les valeurs negatives / charges

### Section header (panels droits)

```cshtml
<div class="card">
    <div class="section-header">
        <i class="bi bi-exclamation-circle"></i>
        <span>A traiter</span>
    </div>
    <div class="empty-state">
        <i class="bi bi-check-circle-fill empty-icon"></i>
        <p class="mb-0">Tout est en ordre</p>
    </div>
</div>
```

### Bouton primary

```cshtml
<a class="btn btn-primary" asp-page="/SalesDocuments/Index">
    <i class="bi bi-plus-lg me-1"></i>Nouveau document
</a>
```

Bleu `#2563eb` plein, ombre subtile, hover `#1d4ed8` avec translateY(-1px).

### Command Palette `Ctrl+K`

16 raccourcis pre-configures vers les modules LigCom. Navigation flechee, Enter pour ouvrir, Esc pour fermer.

### Toasts (intercepte `TempData["StatusMessage"]`)

- Si commence par `Error:` -> rouge
- Sinon -> vert

## Activation

### Voir la demo immediatement

Lancer l'app puis naviguer vers `/DashboardCompta` - la page utilise deja `_LayoutCompta` et reproduit le screenshot LigFin 1:1.

### Globalement (toutes les pages LigCom)

Dans `Pages/_ViewStart.cshtml` :

```cshtml
@{
    Layout = "_LayoutCompta";
}
```

### A/B test par claim

```cshtml
@{
    Layout = User.HasClaim("ui_theme", "v2") ? "_LayoutCompta" : "_Layout";
}
```

## Compatibilite

- Bootstrap 5.3 (CDN)
- Bootstrap Icons 1.11 (CDN)
- Compatible avec les pages Razor existantes : activer le layout sans modifier les pages individuelles
- Composants Bootstrap (cards, tables, btns, dropdowns) automatiquement re-stylises via overrides CSS

## Coherence Lig*** (LigFin + LigCom)

L'objectif est que **les deux applications de la suite Lig*** soient indissociables visuellement** :

| Aspect | LigFin (ComptaSaaS) | LigCom (GescomSaas) |
|---|---|---|
| Sidebar | claire `#fafbfc` | identique |
| Logo | `[carre sombre] LigFin` | `[carre sombre] LigCom` |
| Item actif | bleu pale + border bleu | identique |
| Bouton primary | bleu `#2563eb` | identique |
| Pill "En ligne" | vert | identique |
| Pill "Exercice" / "Tenant" | vert | identique |
| KPI tiles | fond blanc + icone carre colore | identique |
| Chart | barres bleu/saumon | identique |
| Police | Inter / system stack | identique |
| Border-radius | 8-12px | identique |
| Espacement | echelle 4px | identique |

L'utilisateur qui passe de LigFin a LigCom a l'impression d'utiliser **deux modules d'une meme suite**, pas deux apps differentes.
