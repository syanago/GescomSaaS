using GescomSaas.Application.Models;

namespace GescomSaas.Application.Catalog;

public static class CommercialFeatureCatalog
{
    public static IReadOnlyList<FeatureModule> Modules { get; } =
    [
        new(
            "Socle SaaS",
            "Multitenant, abonnements, utilisateurs, editions et securite pour les clients SaaS.",
            [
                "Espaces clients isoles par tenant",
                "Abonnements d'essai, actifs, suspendus ou resilies",
                "Plans Essentials, Standard et Enterprise",
                "Gestion des utilisateurs, roles et proprietaires de tenant"
            ]),
        new(
            "Referentiels",
            "Articles, categories, taxes, tarifs, tiers et conditions de paiement.",
            [
                "Clients, fournisseurs et prospects",
                "Articles stockes, services et nomenclatures",
                "Tarifs standards et listes de prix",
                "Codes taxes, devises et modes de reglement"
            ]),
        new(
            "Ventes",
            "Cycle complet devis > commande > livraison > facture > avoir.",
            [
                "Numerotation par type de document",
                "Statuts brouillon, ouvert, partiellement traite, solde",
                "Conditions de paiement et dates d'echeance",
                "Totaux HT, taxes et TTC sur lignes et entetes"
            ]),
        new(
            "Achats",
            "Demandes, commandes, receptions, factures fournisseurs et avoirs.",
            [
                "Flux achats parallele au flux ventes",
                "Pilotage des receptions et rapprochements",
                "Valorisation des couts d'achat",
                "Preparation du lien avec la comptabilite"
            ]),
        new(
            "Stocks",
            "Depots, mouvements, disponibilites et tracabilite des quantites.",
            [
                "Entrees, sorties, transferts et ajustements",
                "Depot par defaut et depots multiples",
                "Mouvements issus des documents commerciaux",
                "Base pour inventaires et valorisation future"
            ]),
        new(
            "Pilotage",
            "Tableaux de bord, activite recente et lecture rapide de la performance.",
            [
                "Indicateurs temps reel par tenant",
                "Documents recents",
                "Chiffre d'affaires du mois",
                "Preparation des rapports et exports"
            ])
    ];

    public static IReadOnlyList<string> Roadmap { get; } =
    [
        "Migrations EF Core versionnees et pipeline CI/CD",
        "Workflow complet des reglements, relances et echeanciers",
        "Comptabilite auxiliaire et integration Sage/ERP externe",
        "Reporting analytique et tableaux de bord par vendeur",
        "API publique et connecteurs import/export Excel, CSV et EDI"
    ];
}
