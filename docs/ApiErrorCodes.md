# Codes d'erreur de l'API GescomSaas

Reference exhaustive et stable des codes d'erreur retournes par l'API REST. Les integrateurs peuvent brancher leur logique de gestion d'erreur sur la valeur du champ `errorCode` ; cette valeur est **garantie stable** sur toute la duree de vie d'une version majeure de l'API (voir [section Stabilite](#stabilite-et-versioning)).

Toutes les erreurs metier et d'integrite sont retournees au format **ProblemDetails RFC 7807** (`Content-Type: application/problem+json`).

## Reference rapide (52 codes metier + 6 generiques)

| Code | Statut | Domaine |
|---|---|---|
| `ACCESS_PERMISSION_UNKNOWN` | 422 | [Profils d'acces](#profils-dacces) |
| `ACCESS_PROFILE_ASSIGNMENT_INVALID` | 422 | [Profils d'acces](#profils-dacces) |
| `ACCESS_PROFILE_NAME_DUPLICATE` | 422 | [Profils d'acces](#profils-dacces) |
| `ALLOCATION_AMOUNT_INVALID` | 422 | [Reglements](#reglements-et-allocations) |
| `ALLOCATION_EXCEEDS_AVAILABLE` | 422 | [Reglements](#reglements-et-allocations) |
| `ALLOCATION_EXCEEDS_INVOICE_BALANCE` | 422 | [Reglements](#reglements-et-allocations) |
| `BUSINESS_RULE_VIOLATION` | 422 | [Generique](#codes-generiques-toutes-ressources) |
| `CUSTOMER_ACCOUNT_BLOCKED` | 422 | [Reglements](#reglements-et-allocations) |
| `DEPOSIT_NONE_AVAILABLE` | 422 | [Reglements](#reglements-et-allocations) |
| `DOC_INVALID_TRANSITION` | 422 | [Documents commerciaux](#documents-commerciaux) |
| `DOC_MANUAL_NUMBERING_REQUIRED` | 422 | [Documents commerciaux](#documents-commerciaux) |
| `IDENTITY_OPERATION_FAILED` | 422 | [Administration utilisateurs](#administration-utilisateurs-et-invitations) |
| `INTERNAL_ERROR` | 500 | [Generique](#codes-generiques-toutes-ressources) |
| `INVITATION_ALREADY_ACCEPTED` | 422 | [Administration utilisateurs](#administration-utilisateurs-et-invitations) |
| `INVITATION_CANCELLED` | 422 | [Administration utilisateurs](#administration-utilisateurs-et-invitations) |
| `INVITATION_EXPIRED` | 422 | [Administration utilisateurs](#administration-utilisateurs-et-invitations) |
| `INVITATION_PENDING_DUPLICATE` | 422 | [Administration utilisateurs](#administration-utilisateurs-et-invitations) |
| `INVOICE_ALREADY_SETTLED` | 422 | [Reglements](#reglements-et-allocations) |
| `NOT_FOUND` | 404 | [Generique](#codes-generiques-toutes-ressources) |
| `NUMBERING_MODE_UNKNOWN` | 422 | [Numerotation](#numerotation) |
| `OFFLINE_ADMIN_CREATE_FAILED` | 422 | [Sync offline](#synchronisation-offline-localnode) |
| `OFFLINE_ADMIN_PASSWORD_FAILED` | 422 | [Sync offline](#synchronisation-offline-localnode) |
| `OFFLINE_ADMIN_ROLE_FAILED` | 422 | [Sync offline](#synchronisation-offline-localnode) |
| `OFFLINE_ADMIN_UPDATE_FAILED` | 422 | [Sync offline](#synchronisation-offline-localnode) |
| `OFFLINE_CENTRAL_CONNECTION_MISSING` | 422 | [Sync offline](#synchronisation-offline-localnode) |
| `PAYMENT_AMOUNT_EXCEEDS_DOCUMENT_BALANCE` | 422 | [Reglements](#reglements-et-allocations) |
| `PAYMENT_AMOUNT_INVALID` | 422 | [Reglements](#reglements-et-allocations) |
| `PAYMENT_DOCUMENT_INCOMPATIBLE` | 422 | [Reglements](#reglements-et-allocations) |
| `PAYMENT_METHOD_NOT_ALLOWED` | 422 | [Reglements](#reglements-et-allocations) |
| `PAYMENT_NO_COMPATIBLE_DUE_DATE` | 422 | [Reglements](#reglements-et-allocations) |
| `PAYMENT_NO_OPEN_INVOICE` | 422 | [Reglements](#reglements-et-allocations) |
| `PAYMENT_PARTNER_MISMATCH` | 422 | [Reglements](#reglements-et-allocations) |
| `PAYMENT_TARGET_REQUIRED` | 422 | [Reglements](#reglements-et-allocations) |
| `PLATFORM_INVOICE_PERIOD_DUPLICATE` | 422 | [Plateforme](#plateforme-et-facturation-saas) |
| `QUOTA_EXCEEDED` | 402 | [Generique](#codes-generiques-toutes-ressources) |
| `REMINDER_NO_GROUPED_AVAILABLE` | 422 | [Reglements](#reglements-et-allocations) |
| `REMINDER_REQUIRES_SALES_INVOICE` | 422 | [Reglements](#reglements-et-allocations) |
| `STOCK_ADJUSTMENT_QUANTITY_INVALID` | 422 | [Stock](#stock) |
| `STOCK_ADJUSTMENT_TYPE_INVALID` | 422 | [Stock](#stock) |
| `STOCK_DOC_ALREADY_POSTED` | 422 | [Stock](#stock) |
| `STOCK_DOC_NOT_DRAFT` | 422 | [Stock](#stock) |
| `STOCK_DOC_NO_LINES` | 422 | [Stock](#stock) |
| `STOCK_ENTRY_DESTINATION_REQUIRED` | 422 | [Stock](#stock) |
| `STOCK_EXIT_SOURCE_REQUIRED` | 422 | [Stock](#stock) |
| `STOCK_INSUFFICIENT` | 422 | [Stock](#stock) |
| `STOCK_LINE_QUANTITY_INVALID` | 422 | [Stock](#stock) |
| `STOCK_LOT_NUMBER_REQUIRED` | 422 | [Stock](#stock) |
| `STOCK_SERIAL_NUMBER_REQUIRED` | 422 | [Stock](#stock) |
| `STOCK_SERIAL_QUANTITY_MUST_BE_ONE` | 422 | [Stock](#stock) |
| `STOCK_TRANSFER_BOTH_WAREHOUSES_REQUIRED` | 422 | [Stock](#stock) |
| `STOCK_TRANSFER_SAME_WAREHOUSE` | 422 | [Stock](#stock) |
| `TENANT_ACCESS_DENIED` | 403 | [Generique](#codes-generiques-toutes-ressources) |
| `TENANT_MUST_KEEP_OWNER` | 422 | [Administration utilisateurs](#administration-utilisateurs-et-invitations) |
| `TENANT_NO_ACTIVE_SUBSCRIPTION` | 422 | [Plateforme](#plateforme-et-facturation-saas) |
| `TENANT_USAGE_COMPUTATION_FAILED` | 422 | [Plateforme](#plateforme-et-facturation-saas) |
| `USER_ALREADY_LINKED_TO_OTHER_TENANT` | 422 | [Administration utilisateurs](#administration-utilisateurs-et-invitations) |
| `USER_ALREADY_LINKED_TO_THIS_TENANT` | 422 | [Administration utilisateurs](#administration-utilisateurs-et-invitations) |
| `USER_INVALID_ROLES` | 422 | [Administration utilisateurs](#administration-utilisateurs-et-invitations) |
| `VALIDATION_FAILED` | 400 | [Generique](#codes-generiques-toutes-ressources) |

## Structure de reponse

```jsonc
{
  "type": "https://httpstatuses.com/422",
  "title": "Regle metier violee",
  "status": 422,
  "detail": "Stock insuffisant pour SKU-001. Disponible : 0.",
  "instance": "/api/inventory/adjustments",
  "errorCode": "STOCK_INSUFFICIENT",
  "correlationId": "0HNL3UK0QSQN3",
  "timestamp": "2026-04-26T14:23:11.123Z",

  // Champs additionnels selon le type d'erreur
  "sku": "SKU-001",
  "available": 0,
  "requested": 5
}
```

| Champ | Description |
|---|---|
| `status` | Code HTTP RFC 7231 |
| `errorCode` | **Code stable** a utiliser cote client pour le branchement metier (ex: afficher un upsell sur `QUOTA_EXCEEDED`) |
| `detail` | Message lisible en francais, **non stable** - peut changer entre versions |
| `instance` | Chemin de la requete declenchante |
| `correlationId` | TraceIdentifier ASP.NET, a fournir au support pour retrouver les logs |
| `timestamp` | UTC, ISO 8601 |
| Extensions specifiques | Voir tableau ci-dessous (ex: `quotaName`, `sku`, `available`...) |

> **Bon usage :** branchez votre logique sur `errorCode`, jamais sur `detail`. Les codes sont garantis stables ; les messages peuvent etre traduits ou raffines.

## Statuts HTTP utilises

| Code | Signification |
|---|---|
| **400** | `ValidationException` - donnees d'entree invalides (avec `errors` par champ) |
| **402** | `QuotaExceededException` - quota du plan SaaS depasse, suggerer un upgrade |
| **403** | `TenantAccessDeniedException` - acces cross-tenant ou tenant absent du contexte |
| **404** | `NotFoundException` - ressource inexistante (ou inaccessible pour ce tenant) |
| **422** | `BusinessRuleException` - regle metier violee (transition impossible, etc.) |
| **500** | Erreur non geree (`errorCode: INTERNAL_ERROR`) - les details sont masques en production |

> Note securitaire : les ressources d'un autre tenant retournent `404` plutot que `403` pour empecher l'enumeration des IDs entre tenants.

## Stabilite et versioning

### Engagement de stabilite

| Element | Garantie |
|---|---|
| `errorCode` | **Stable** sur toute la duree d'une version majeure de l'API. Un code n'est jamais renomme ; au pire, il est marque deprecie et remplace par un nouveau code |
| `status` HTTP associe a un code | **Stable** |
| `detail` | **Non stable** - peut etre traduit, raffine, ou modifie sans preavis |
| Cle d'extension (`quotaName`, `sku`, `errors`...) | **Stable** une fois publiee |
| Nouveaux codes | Peuvent etre ajoutes a tout moment - les clients doivent traiter un code inconnu comme une erreur generique selon le `status` |

### Lien avec [API Versioning](./ApiVersioning.md)

L'API suit le versioning `Asp.Versioning` (path `/api/v1/`, header `api-version`, querystring `?api-version`). Les codes d'erreur **survivent au passage de version** : si un code `STOCK_INSUFFICIENT` est valide en v1, il est aussi valide en v2 (sauf changement de comportement metier explicite documente dans le changelog v2).

### Erreurs emises par le framework de versioning

Quand un client appelle une version inexistante (ex: `/api/v99/...`), l'erreur **n'est pas** une `AppException` mais un ProblemDetails standard genere par `Asp.Versioning` :

```json
{
  "type": "https://docs.api-versioning.org/problems#unsupported",
  "title": "Unsupported API version",
  "status": 400,
  "detail": "The HTTP resource that matches the request URI '/api/v99/...' does not support the API version '99.0'."
}
```

Ces reponses **n'ont pas** de champ `errorCode`. Branchez votre logique sur le `type` ou directement sur `status: 400` + headers `api-supported-versions`.

## Decouvrir les codes

| Source | Usage |
|---|---|
| **Ce document** | Reference exhaustive, organisee par domaine. Le canon |
| **Swagger / OpenAPI** | `https://<host>/swagger` - liste les endpoints + structure ProblemDetails. Les codes specifiques par endpoint **ne sont pas** annotes dans le schema OpenAPI : referez-vous a ce document pour la matrice endpoint <-> codes possibles |
| **Header `X-Correlation-Id`** | Present dans toute reponse - a fournir au support pour retrouver les logs Serilog correspondants |
| **Headers `api-supported-versions` / `api-deprecated-versions`** | Presents dans toute reponse de `/api/v*/...` |

---

## Codes generiques (toutes ressources)

| Code | Statut | Quand |
|---|---|---|
| `NOT_FOUND` | 404 | Ressource introuvable. Extensions: `entity` |
| `VALIDATION_FAILED` | 400 | Erreurs par champ dans `errors` |
| `BUSINESS_RULE_VIOLATION` | 422 | Code par defaut quand un service ne fournit pas de code custom |
| `QUOTA_EXCEEDED` | 402 | Extensions: `quotaName`, `limit`, `current` |
| `TENANT_ACCESS_DENIED` | 403 | Tenant absent ou acces cross-tenant |
| `INTERNAL_ERROR` | 500 | Exception non geree. `correlationId` indispensable au support |

### Focus sur `VALIDATION_FAILED` (400)

Les erreurs **400** proviennent de deux sources, toutes deux retournees au meme format :

1. **FluentValidation** sur les DTO d'entree ([voir docs/Validation.md](./Validation.md))
2. **Services metier** sur les regles de forme simples (champs obligatoires non testables sans la DB)

Format de reponse standard (`ValidationProblemDetails` RFC 7807) :

```json
{
  "type": "https://httpstatuses.com/400",
  "title": "Donnees invalides",
  "status": 400,
  "detail": "Une ou plusieurs erreurs de validation sont survenues.",
  "instance": "/api/v1/inventory/adjustments",
  "errorCode": "VALIDATION_FAILED",
  "correlationId": "0HNL3UK0QSQN3",
  "errors": {
    "Quantity": [ "La quantite doit etre strictement positive." ],
    "ProductId": [ "L'article est obligatoire." ]
  }
}
```

> **Bon usage :** afficher les messages de `errors[champ]` directement sous chaque input du formulaire. Le `errorCode: VALIDATION_FAILED` permet de distinguer ce 400 d'un eventuel 400 emis par `Asp.Versioning` (pas de `errorCode` dans ce cas).

---

## Codes par domaine

### Documents commerciaux

| Code | Statut | Service | Description |
|---|---|---|---|
| `DOC_INVALID_TRANSITION` | 422 | `CommercialDocumentWorkflowService` | Transformation devis-commande-livraison-facture interdite. `detail` contient la transition tentee |
| `DOC_MANUAL_NUMBERING_REQUIRED` | 422 | `CommercialDocumentWorkflowService` | Numerotation en mode manuel : il faut creer le document via l'ecran Nouveau pour saisir le numero |

### Stock

| Code | Statut | Service | Extensions |
|---|---|---|---|
| `STOCK_INSUFFICIENT` | 422 | `InventoryService` | `sku`, `available`, `requested` |
| `STOCK_LOT_NUMBER_REQUIRED` | 422 | `InventoryService` | `sku` (article gere par lot, lot manquant) |
| `STOCK_SERIAL_NUMBER_REQUIRED` | 422 | `InventoryService` | `sku` (article gere par numero de serie) |
| `STOCK_SERIAL_QUANTITY_MUST_BE_ONE` | 422 | `InventoryService` | `sku` (1 ligne = 1 numero de serie) |
| `STOCK_ADJUSTMENT_QUANTITY_INVALID` | 422 | `InventoryService` | Quantite <= 0 |
| `STOCK_ADJUSTMENT_TYPE_INVALID` | 422 | `InventoryService` | Type de mouvement non-ajustement |
| `STOCK_LINE_QUANTITY_INVALID` | 422 | `InventoryService` | Ligne de document avec quantite <= 0 |
| `STOCK_DOC_ALREADY_POSTED` | 422 | `InventoryService` | Document deja valide |
| `STOCK_DOC_NO_LINES` | 422 | `InventoryService` | Document sans aucune ligne |
| `STOCK_DOC_NOT_DRAFT` | 422 | `StockDocumentService` | Seuls les brouillons peuvent etre valides |
| `STOCK_ENTRY_DESTINATION_REQUIRED` | 422 | `InventoryService` | Entree sans depot de destination |
| `STOCK_EXIT_SOURCE_REQUIRED` | 422 | `InventoryService` | Sortie sans depot source |
| `STOCK_TRANSFER_BOTH_WAREHOUSES_REQUIRED` | 422 | `InventoryService` | Transfert sans source ou destination |
| `STOCK_TRANSFER_SAME_WAREHOUSE` | 422 | `InventoryService` | Transfert avec source == destination |

### Reglements et allocations

| Code | Statut | Service | Description |
|---|---|---|---|
| `PAYMENT_AMOUNT_INVALID` | 422 | `SettlementService` | Montant <= 0 |
| `PAYMENT_AMOUNT_EXCEEDS_DOCUMENT_BALANCE` | 422 | `SettlementService` | Montant > solde restant |
| `PAYMENT_METHOD_NOT_ALLOWED` | 422 | `SettlementService` | Mode de reglement interdit pour ce tenant |
| `PAYMENT_TARGET_REQUIRED` | 422 | `SettlementService` | Choisir facture, auto-affectation ou acompte |
| `PAYMENT_NO_OPEN_INVOICE` | 422 | `SettlementService` | Aucune facture ouverte pour auto-affectation |
| `PAYMENT_NO_COMPATIBLE_DUE_DATE` | 422 | `SettlementService` | Aucune echeance compatible |
| `PAYMENT_PARTNER_MISMATCH` | 422 | `SettlementService` | Reglement et facture sur tiers differents |
| `PAYMENT_DOCUMENT_INCOMPATIBLE` | 422 | `SettlementService` | Type de document incompatible avec direction du reglement |
| `ALLOCATION_AMOUNT_INVALID` | 422 | `SettlementService` | Montant a affecter <= 0 |
| `ALLOCATION_EXCEEDS_AVAILABLE` | 422 | `SettlementService` | Montant > disponible sur le reglement |
| `ALLOCATION_EXCEEDS_INVOICE_BALANCE` | 422 | `SettlementService` | Montant > solde restant de la facture |
| `INVOICE_ALREADY_SETTLED` | 422 | `SettlementService` | Facture deja totalement reglee |
| `DEPOSIT_NONE_AVAILABLE` | 422 | `SettlementService` | Aucun acompte disponible pour ce client |
| `CUSTOMER_ACCOUNT_BLOCKED` | 422 | `SettlementService` | Plafond credit depasse ou impayes - operation bloquee |
| `REMINDER_REQUIRES_SALES_INVOICE` | 422 | `SettlementService` | Relance disponible uniquement sur facture client |
| `REMINDER_NO_GROUPED_AVAILABLE` | 422 | `SettlementService` | Aucune relance groupee disponible |

### Numerotation

| Code | Statut | Service | Description |
|---|---|---|---|
| `NUMBERING_MODE_UNKNOWN` | 422 | `NumberingService` | Mode de numerotation non reconnu |

### Plateforme et facturation SaaS

| Code | Statut | Service | Description |
|---|---|---|---|
| `TENANT_NO_ACTIVE_SUBSCRIPTION` | 422 | `PlatformAdministrationService` | Aucun abonnement exploitable pour ce tenant |
| `TENANT_USAGE_COMPUTATION_FAILED` | 422 | `PlatformAdministrationService` | Calcul des usages impossible |
| `PLATFORM_INVOICE_PERIOD_DUPLICATE` | 422 | `PlatformAdministrationService` | Facture plateforme deja existante pour la periode |

### Administration utilisateurs et invitations

| Code | Statut | Service | Description |
|---|---|---|---|
| `USER_ALREADY_LINKED_TO_OTHER_TENANT` | 422 | `PlatformUserAdministrationService` | Utilisateur deja rattache a un autre tenant |
| `USER_ALREADY_LINKED_TO_THIS_TENANT` | 422 | `PlatformUserAdministrationService` | Utilisateur deja rattache a ce tenant |
| `USER_INVALID_ROLES` | 422 | `PlatformUserAdministrationService` | Roles invalides. Extensions: `invalidRoles[]` |
| `TENANT_MUST_KEEP_OWNER` | 422 | `PlatformUserAdministrationService` | Le tenant doit conserver au moins un TenantOwner |
| `INVITATION_PENDING_DUPLICATE` | 422 | `PlatformUserAdministrationService` | Invitation en cours pour cet email |
| `INVITATION_ALREADY_ACCEPTED` | 422 | `PlatformUserAdministrationService` | Invitation deja acceptee |
| `INVITATION_CANCELLED` | 422 | `PlatformUserAdministrationService` | Invitation annulee |
| `INVITATION_EXPIRED` | 422 | `PlatformUserAdministrationService` | Invitation expiree |
| `IDENTITY_OPERATION_FAILED` | 422 | `PlatformUserAdministrationService` | Echec d'une operation ASP.NET Identity. Extensions: `identityErrors[]` |

### Profils d'acces

| Code | Statut | Service | Description |
|---|---|---|---|
| `ACCESS_PROFILE_NAME_DUPLICATE` | 422 | `TenantAccessProfileService` | Un profil portant ce nom existe deja |
| `ACCESS_PROFILE_ASSIGNMENT_INVALID` | 422 | `TenantAccessProfileService` | Une affectation cible un profil invalide |
| `ACCESS_PERMISSION_UNKNOWN` | 422 | `TenantAccessProfileService` | Permissions inconnues. Extensions: `unknownPermissions[]` |

### Synchronisation offline (LocalNode)

| Code | Statut | Service | Description |
|---|---|---|---|
| `OFFLINE_ADMIN_CREATE_FAILED` | 422 | `OfflineSyncService` | Echec creation administrateur local |
| `OFFLINE_ADMIN_UPDATE_FAILED` | 422 | `OfflineSyncService` | Echec mise a jour administrateur local |
| `OFFLINE_ADMIN_PASSWORD_FAILED` | 422 | `OfflineSyncService` | Echec changement mot de passe |
| `OFFLINE_ADMIN_ROLE_FAILED` | 422 | `OfflineSyncService` | Echec affectation role administrateur |
| `OFFLINE_CENTRAL_CONNECTION_MISSING` | 422 | `OfflineSyncService` | Connexion SQL Server centrale non configuree |

---

## Exemples de gestion cote client

### React / TypeScript

```ts
type AppError = {
  status: number;
  errorCode: string;
  detail: string;
  correlationId: string;
  // extensions
  quotaName?: string;
  limit?: number;
  current?: number;
  sku?: string;
  available?: number;
  errors?: Record<string, string[]>;
};

async function postPayment(payload: unknown) {
  const res = await fetch("/api/payments", { method: "POST", body: JSON.stringify(payload) });
  if (res.ok) return res.json();

  const err: AppError = await res.json();

  switch (err.errorCode) {
    case "QUOTA_EXCEEDED":
      // Afficher un upsell : quota X/Y atteint sur le plan Z
      showUpgradeBanner({ quota: err.quotaName, limit: err.limit, current: err.current });
      break;

    case "STOCK_INSUFFICIENT":
      toast.error(`Stock insuffisant pour ${err.sku} (dispo : ${err.available})`);
      break;

    case "INVOICE_ALREADY_SETTLED":
      // Rafraichir l'etat de la facture, masquer le bouton
      reloadInvoice();
      break;

    case "VALIDATION_FAILED":
      // Afficher les erreurs par champ
      setFieldErrors(err.errors ?? {});
      break;

    case "TENANT_ACCESS_DENIED":
      router.push("/forbidden");
      break;

    default:
      toast.error(err.detail || "Une erreur est survenue.");
      console.error("[API]", err.correlationId, err);
  }
}
```

### .NET / HttpClient

```csharp
var response = await client.PostAsJsonAsync("/api/payments", payload);
if (response.IsSuccessStatusCode)
{
    return await response.Content.ReadFromJsonAsync<PaymentDto>();
}

var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
var code = problem.GetProperty("errorCode").GetString();

return code switch
{
    "QUOTA_EXCEEDED" => throw new UpsellException(
        problem.GetProperty("quotaName").GetString()!,
        problem.GetProperty("limit").GetInt32(),
        problem.GetProperty("current").GetInt32()),
    "STOCK_INSUFFICIENT" => throw new StockException(
        problem.GetProperty("sku").GetString()!,
        problem.GetProperty("available").GetDecimal()),
    _ => throw new ApiException(code, problem.GetProperty("detail").GetString())
};
```

---

## Ajouter un nouveau code

1. Choisir un prefixe metier coherent (ex: `STOCK_*`, `PAYMENT_*`, `DOC_*`).
2. Lever via `BusinessRuleException` (ou un sous-type approprie) :
   ```csharp
   throw new BusinessRuleException("Message lisible.", errorCode: "MON_NOUVEAU_CODE");
   ```
3. Si des metadonnees sont utiles cote client, les ajouter dans `ex.Data["..."]` :
   ```csharp
   var ex = new BusinessRuleException("...", errorCode: "MON_CODE");
   ex.Data["champUtile"] = valeur;
   throw ex;
   ```
   > Note : actuellement seuls les codes specifiques (`QUOTA_EXCEEDED`, `NOT_FOUND`) ont leurs extensions automatiquement promues vers `ProblemDetails.Extensions` par le middleware. Pour les autres, etendre `GlobalExceptionMiddleware.AddExtraDetails`.
4. Ajouter une ligne dans ce document, dans la section appropriee.
5. Ecrire un test d'integration qui valide le code et le statut HTTP.

## Bibliotheque de code TypeScript prete a copier

Constantes typees pour le branchement client - copier-coller directement :

```ts
/**
 * Codes d'erreur stables retournes par l'API GescomSaas.
 * Cette liste est synchronisee avec docs/ApiErrorCodes.md.
 */
export const GescomErrorCode = {
  // Generique
  NOT_FOUND: "NOT_FOUND",
  VALIDATION_FAILED: "VALIDATION_FAILED",
  BUSINESS_RULE_VIOLATION: "BUSINESS_RULE_VIOLATION",
  QUOTA_EXCEEDED: "QUOTA_EXCEEDED",
  TENANT_ACCESS_DENIED: "TENANT_ACCESS_DENIED",
  INTERNAL_ERROR: "INTERNAL_ERROR",
  // Documents commerciaux
  DOC_INVALID_TRANSITION: "DOC_INVALID_TRANSITION",
  DOC_MANUAL_NUMBERING_REQUIRED: "DOC_MANUAL_NUMBERING_REQUIRED",
  // Stock
  STOCK_INSUFFICIENT: "STOCK_INSUFFICIENT",
  STOCK_LOT_NUMBER_REQUIRED: "STOCK_LOT_NUMBER_REQUIRED",
  STOCK_SERIAL_NUMBER_REQUIRED: "STOCK_SERIAL_NUMBER_REQUIRED",
  STOCK_SERIAL_QUANTITY_MUST_BE_ONE: "STOCK_SERIAL_QUANTITY_MUST_BE_ONE",
  STOCK_ADJUSTMENT_QUANTITY_INVALID: "STOCK_ADJUSTMENT_QUANTITY_INVALID",
  STOCK_ADJUSTMENT_TYPE_INVALID: "STOCK_ADJUSTMENT_TYPE_INVALID",
  STOCK_LINE_QUANTITY_INVALID: "STOCK_LINE_QUANTITY_INVALID",
  STOCK_DOC_ALREADY_POSTED: "STOCK_DOC_ALREADY_POSTED",
  STOCK_DOC_NO_LINES: "STOCK_DOC_NO_LINES",
  STOCK_DOC_NOT_DRAFT: "STOCK_DOC_NOT_DRAFT",
  STOCK_ENTRY_DESTINATION_REQUIRED: "STOCK_ENTRY_DESTINATION_REQUIRED",
  STOCK_EXIT_SOURCE_REQUIRED: "STOCK_EXIT_SOURCE_REQUIRED",
  STOCK_TRANSFER_BOTH_WAREHOUSES_REQUIRED: "STOCK_TRANSFER_BOTH_WAREHOUSES_REQUIRED",
  STOCK_TRANSFER_SAME_WAREHOUSE: "STOCK_TRANSFER_SAME_WAREHOUSE",
  // Reglements
  PAYMENT_AMOUNT_INVALID: "PAYMENT_AMOUNT_INVALID",
  PAYMENT_AMOUNT_EXCEEDS_DOCUMENT_BALANCE: "PAYMENT_AMOUNT_EXCEEDS_DOCUMENT_BALANCE",
  PAYMENT_METHOD_NOT_ALLOWED: "PAYMENT_METHOD_NOT_ALLOWED",
  PAYMENT_TARGET_REQUIRED: "PAYMENT_TARGET_REQUIRED",
  PAYMENT_NO_OPEN_INVOICE: "PAYMENT_NO_OPEN_INVOICE",
  PAYMENT_NO_COMPATIBLE_DUE_DATE: "PAYMENT_NO_COMPATIBLE_DUE_DATE",
  PAYMENT_PARTNER_MISMATCH: "PAYMENT_PARTNER_MISMATCH",
  PAYMENT_DOCUMENT_INCOMPATIBLE: "PAYMENT_DOCUMENT_INCOMPATIBLE",
  ALLOCATION_AMOUNT_INVALID: "ALLOCATION_AMOUNT_INVALID",
  ALLOCATION_EXCEEDS_AVAILABLE: "ALLOCATION_EXCEEDS_AVAILABLE",
  ALLOCATION_EXCEEDS_INVOICE_BALANCE: "ALLOCATION_EXCEEDS_INVOICE_BALANCE",
  INVOICE_ALREADY_SETTLED: "INVOICE_ALREADY_SETTLED",
  DEPOSIT_NONE_AVAILABLE: "DEPOSIT_NONE_AVAILABLE",
  CUSTOMER_ACCOUNT_BLOCKED: "CUSTOMER_ACCOUNT_BLOCKED",
  REMINDER_REQUIRES_SALES_INVOICE: "REMINDER_REQUIRES_SALES_INVOICE",
  REMINDER_NO_GROUPED_AVAILABLE: "REMINDER_NO_GROUPED_AVAILABLE",
  // Numerotation
  NUMBERING_MODE_UNKNOWN: "NUMBERING_MODE_UNKNOWN",
  // Plateforme
  TENANT_NO_ACTIVE_SUBSCRIPTION: "TENANT_NO_ACTIVE_SUBSCRIPTION",
  TENANT_USAGE_COMPUTATION_FAILED: "TENANT_USAGE_COMPUTATION_FAILED",
  PLATFORM_INVOICE_PERIOD_DUPLICATE: "PLATFORM_INVOICE_PERIOD_DUPLICATE",
  // Administration utilisateurs
  USER_ALREADY_LINKED_TO_OTHER_TENANT: "USER_ALREADY_LINKED_TO_OTHER_TENANT",
  USER_ALREADY_LINKED_TO_THIS_TENANT: "USER_ALREADY_LINKED_TO_THIS_TENANT",
  USER_INVALID_ROLES: "USER_INVALID_ROLES",
  TENANT_MUST_KEEP_OWNER: "TENANT_MUST_KEEP_OWNER",
  INVITATION_PENDING_DUPLICATE: "INVITATION_PENDING_DUPLICATE",
  INVITATION_ALREADY_ACCEPTED: "INVITATION_ALREADY_ACCEPTED",
  INVITATION_CANCELLED: "INVITATION_CANCELLED",
  INVITATION_EXPIRED: "INVITATION_EXPIRED",
  IDENTITY_OPERATION_FAILED: "IDENTITY_OPERATION_FAILED",
  // Profils d'acces
  ACCESS_PROFILE_NAME_DUPLICATE: "ACCESS_PROFILE_NAME_DUPLICATE",
  ACCESS_PROFILE_ASSIGNMENT_INVALID: "ACCESS_PROFILE_ASSIGNMENT_INVALID",
  ACCESS_PERMISSION_UNKNOWN: "ACCESS_PERMISSION_UNKNOWN",
  // Sync offline
  OFFLINE_ADMIN_CREATE_FAILED: "OFFLINE_ADMIN_CREATE_FAILED",
  OFFLINE_ADMIN_UPDATE_FAILED: "OFFLINE_ADMIN_UPDATE_FAILED",
  OFFLINE_ADMIN_PASSWORD_FAILED: "OFFLINE_ADMIN_PASSWORD_FAILED",
  OFFLINE_ADMIN_ROLE_FAILED: "OFFLINE_ADMIN_ROLE_FAILED",
  OFFLINE_CENTRAL_CONNECTION_MISSING: "OFFLINE_CENTRAL_CONNECTION_MISSING",
} as const;

export type GescomErrorCode = typeof GescomErrorCode[keyof typeof GescomErrorCode];

export interface GescomProblemDetails {
  type: string;
  title: string;
  status: number;
  detail: string;
  instance: string;
  errorCode: GescomErrorCode | string;
  correlationId: string;
  timestamp: string;
  // Extensions communes
  entity?: string;            // sur NOT_FOUND
  errors?: Record<string, string[]>; // sur VALIDATION_FAILED
  quotaName?: string;         // sur QUOTA_EXCEEDED
  limit?: number;
  current?: number;
  sku?: string;               // sur STOCK_*
  available?: number;
  requested?: number;
  invalidRoles?: string[];    // sur USER_INVALID_ROLES
  unknownPermissions?: string[]; // sur ACCESS_PERMISSION_UNKNOWN
  identityErrors?: string[];  // sur IDENTITY_OPERATION_FAILED
}
```

## Changelog des codes

| Date | Version API | Changement |
|---|---|---|
| 2026-04-26 | v1.0 | Liste initiale - 52 codes metier publies + 6 generiques |

> Quand un nouveau code est ajoute, marquer la date et la version d'API ; quand un code est deprecie, indiquer le code de remplacement et la date de retrait planifiee.

## References

- [RFC 7807 - Problem Details for HTTP APIs](https://www.rfc-editor.org/rfc/rfc7807)
- [Versioning de l'API REST](./ApiVersioning.md)
- [Validation des entrees (FluentValidation)](./Validation.md)
- [Pipeline d'observabilite (correlationId, logs)](./Observability.md)
- Middleware : [`src/GescomSaas.Web/Middleware/GlobalExceptionMiddleware.cs`](../src/GescomSaas.Web/Middleware/GlobalExceptionMiddleware.cs)
- Hierarchie d'exceptions : [`src/GescomSaas.Domain/Exceptions/`](../src/GescomSaas.Domain/Exceptions/)
- Tests : [`tests/GescomSaas.Tests/Middleware/GlobalExceptionMiddlewareTests.cs`](../tests/GescomSaas.Tests/Middleware/GlobalExceptionMiddlewareTests.cs)
