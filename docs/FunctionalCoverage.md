# Couverture fonctionnelle cible

## Objectif

Construire une gestion commerciale SaaS en C# / SQL Server inspirée de Sage Gescom 100, avec une logique modulaire et multi-tenant.

## Modules metier

### 1. Administration SaaS

- inscription et activation des tenants
- editions d'abonnement
- suivi des essais, suspensions, renouvellements
- gestion des utilisateurs, roles et proprietaires de compte
- invitations, acceptation en self-service et rattachement manuel de comptes existants
- quotas par plan et overrides par tenant
- quotas bloquants en temps reel sur les creations et activations metier
- facturation plateforme et suivi des echeances SaaS
- generation PDF des factures plateforme
- audit, journalisation et supervision

### 2. Referentiels

- fiches clients, fournisseurs et prospects
- catalogues articles, services et familles
- depots et emplacements
- taxes, devises, conditions de paiement
- tarifs standards et remises

### 3. Ventes

- devis
- commandes clients
- bons de livraison
- factures
- avoirs
- numerotation et statuts

### 4. Achats

- demandes d'achat
- commandes fournisseurs
- receptions
- factures fournisseurs
- avoirs fournisseurs

### 5. Stocks

- stock initial
- entrees et sorties
- transferts inter-depots
- ajustements
- inventaires
- valorisation future

### 6. Reglements et finance

- echeanciers
- encaissements
- decaissements
- relances
- export comptable

### 7. Pilotage

- tableaux de bord
- statistiques de vente et achat
- top articles / clients
- marge et rotation de stock

## Etat actuel du socle

- architecture multi-projets Visual Studio
- modele de donnees principal en place
- seed de demonstration
- authentification ASP.NET Core Identity
- dashboard web initial

## Prochaines iterations

1. CRUD complets sur clients, fournisseurs, articles et depots
2. generation des documents avec workflows inter-documents
3. reservation et decrement automatique des stocks
4. gestion des reglements et balances
5. migrations EF Core, tests et pipeline de deploiement
