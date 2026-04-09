# GescomSaas

Base de solution Visual Studio pour une application web de gestion commerciale SaaS inspiree de la logique de Sage Gescom 100.

## Stack

- .NET 9
- ASP.NET Core Razor Pages
- C#
- SQL Server / LocalDB
- Entity Framework Core
- ASP.NET Core Identity

## Structure

- `src/GescomSaas.Domain` : entites metier et enums
- `src/GescomSaas.Application` : contrats, DTOs et catalogue fonctionnel
- `src/GescomSaas.Infrastructure` : persistence EF Core, Identity, seed et services
- `src/GescomSaas.Web` : interface web Razor Pages

## Fonctionnalites posees

- SaaS multi-tenant
- Plans d'abonnement, gestion detaillee des utilisateurs par tenant, invitations et roles
- Clients, fournisseurs, articles, taxes, tarifs, depots
- Documents commerciaux vente et achat
- Mouvements de stock
- Tableau de bord de synthese
- Administration SaaS avancee: tenants, plans, quotas et facturation plateforme
- PDF des factures plateforme
- Quotas bloquants sur utilisateurs, clients, fournisseurs, articles, depots et documents

## Demarrage

1. Ouvrir `GescomSaas.sln` dans Visual Studio.
2. Definir `src/GescomSaas.Web` comme projet de demarrage.
3. Lancer l'application.
4. La base SQL Server LocalDB `GescomSaas` sera initialisee automatiquement au premier demarrage.

## Compte de demonstration

- Identifiant : `owner@demo.gescom.local`
- Mot de passe : `Demo123!`

## Compte plateforme

- Identifiant : `platform@gescom.local`
- Mot de passe : `Demo123!`

## Comptes et invitations de demo

- Utilisateur rattache : `sales@demo.gescom.local / Demo123!`
- Utilisateur libre a rattacher : `consultant@gescom.local / Demo123!`
- Invitation en attente : `buyer@demo.gescom.local`

## Notes

- La base est creee via `EnsureCreated` pour accelerer le bootstrap initial.
- Si le modele evolue fortement, il est recommande de regenerer la base pour aligner le schema.
- La prochaine etape recommandee est la mise en place de migrations EF Core versionnees et des ecrans CRUD par module.
- Le cadrage fonctionnel est detaille dans `docs/FunctionalCoverage.md`.
- Le design system et la direction UI de la branche `UI_UX` sont poses dans `docs/DesignSystem.md`.
