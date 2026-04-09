# Design System LigCom

## Intention

Le design system `LigCom` vise un rendu B2B premium, lisible et orienté action pour un SaaS de gestion commerciale utilisé au quotidien par des profils ventes, achats, finance, stock et direction.

Les principes directeurs sont :

- comprendre l'etat du business en quelques secondes
- accelerer les actions recurrentes
- rendre les ecrans denses plus lisibles sans perdre de richesse fonctionnelle
- maintenir une coherence visuelle entre cockpit, listes, formulaires et administration SaaS

## Tokens

### Couleurs

- `--bg-ink`, `--bg-deep` : arriere-plans globaux
- `--surface`, `--surface-soft`, `--surface-raised` : surfaces applicatives
- `--border-soft`, `--border-strong` : bordures et separations
- `--accent`, `--accent-strong`, `--mint` : accents d'action et repères
- `--success`, `--warning`, `--danger` : statuts et alertes
- `--text-main`, `--text-soft`, `--text-faint` : hierarchie de lecture

### Rayon / Ombres

- `--radius-sm` : petites capsules et badges
- `--radius-md` : cartes secondaires et sous-panneaux
- `--radius-lg` : panneaux principaux
- `--radius-xl` : hero, grandes cartes et shell
- `--shadow`, `--shadow-soft` : profondeur globale et legere

### Espacements

- `--space-1` a `--space-8`
- usage recommande :
  - `1` a `3` pour micro-espacements
  - `4` a `5` pour composants standards
  - `6` a `8` pour zones principales et hero

## Composants

### App Shell

- `topbar-shell`
- `brand-mark`
- `tenant-context`
- `nav-cluster`
- `auth-actions`

Usage :
- presenter l'identite `LigCom`
- garder une lecture immediate du tenant courant
- rendre la navigation stable, compacte et scannable

### Workspace Header

- `workspace-hero`
- `workspace-shell`
- `workspace-kicker`
- `workspace-actions`
- `workspace-summary-grid`
- `workspace-summary-card`

Usage :
- chaque page de niveau 1 ou 2 doit commencer par un header orienté action
- le header combine contexte, resume, CTA et filtres

### Segmented Navigation

- `segment-bar`
- `segment-link`

Usage :
- navigation entre sous-types de documents
- vues rapides type `Devis`, `Commandes`, `Factures`

### Data Card

- `panel-card`
- `panel-card-raised`
- `metric-card`
- `kpi-tile`
- `signal-item`

Usage :
- une seule logique de carte, declinée selon l'importance

### Table Workspace

- `workspace-table`
- `doc-number`
- `doc-meta`
- `row-actions`

Usage :
- faciliter le scan visuel
- faire ressortir numero, tiers, statut et montant

### Settings Layout

- `settings-shell`
- `settings-form-panel`
- `settings-preview-panel`
- `preview-stack`
- `preview-card`

Usage :
- toujours afficher un apercu du rendu lorsque l'utilisateur modifie des parametres globaux

## Bonnes pratiques LigCom

- toujours proposer un CTA principal visible sans scroll sur les ecrans d'action
- limiter les headers a une promesse claire et une phrase de contexte
- utiliser les badges d'etat pour informer, pas pour decorer
- privilegier les actions contextuelles sur ligne plutot que multiplier les ecrans
- garder les metadonnees en texte secondaire et les chiffres critiques en fort contraste
- sur formulaire complexe, accompagner chaque groupe d'un exemple concret ou d'un apercu

## Ecrans prioritaires

1. `Cockpit` : centre d'action et de decision
2. `Ventes` / `Achats` : workspaces documentaires
3. `Parametres > Societe et formats` : configuration guidee avec apercu
4. `Fiche document` : resume + actions + historique

## Themes

### LigCom Nuit

- theme dense et contraste fort
- approprie pour usage pro intensif

### LigCom Vert Clair

- base claire inspiree majoritairement de `vert 2`
- accents emeraude et dores inspires de `vert 1`
- meilleur confort de lecture en journee
