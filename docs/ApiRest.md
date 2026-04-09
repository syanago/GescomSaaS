# API REST Gescom SaaS

## Authentification

### Interface interactive

Swagger UI est disponible sur :

- `https://localhost:{port}/swagger`

### Flux Swagger recommande

1. Appeler `POST /api/identity/login?useCookies=false`
2. Recuperer `accessToken` et `refreshToken`
3. Cliquer sur `Authorize` dans Swagger
4. Coller uniquement la valeur du token d'acces
5. Tester les routes protegees `/api/v1/...`

Exemple de corps JSON :

```json
{
  "email": "owner@demo.gescom.local",
  "password": "Demo123!"
}
```

- `useCookies=false` : bearer token pour API / mobile
- `useCookies=true` : cookie pour navigateur

### Login API

`POST /api/identity/login?useCookies=false`

Reponse attendue :

```json
{
  "tokenType": "Bearer",
  "accessToken": "...",
  "expiresIn": 3600,
  "refreshToken": "..."
}
```

### Refresh token

`POST /api/identity/refresh`

### Logout API

`POST /api/auth/logout`

## Endpoints principaux

### Contexte

- `GET /api/v1/context`
- `GET /api/v1/dashboard`

Le endpoint `GET /api/v1/context` retourne aussi :

- les roles du compte connecte
- les quotas du tenant avec `used`, `limit`, `remaining` et `isExceeded`
- le nombre de quotas depasses via `exceededQuotaCount`

### Tiers

- `GET /api/v1/partners?scope=all|customers|suppliers`
- `GET /api/v1/partners/{id}`
- `POST /api/v1/partners`
- `PUT /api/v1/partners/{id}`
- `DELETE /api/v1/partners/{id}`

### Articles

- `GET /api/v1/products`
- `GET /api/v1/products?trackedOnly=true`
- `GET /api/v1/products/{id}`
- `POST /api/v1/products`
- `PUT /api/v1/products/{id}`
- `DELETE /api/v1/products/{id}`

### Depots

- `GET /api/v1/warehouses`

### Documents commerciaux

- `GET /api/v1/documents`
- `GET /api/v1/documents?family=sales`
- `GET /api/v1/documents?family=purchases`
- `GET /api/v1/documents?type=SalesInvoice`
- `GET /api/v1/documents/{id}`
- `POST /api/v1/documents`
- `PUT /api/v1/documents/{id}`
- `POST /api/v1/documents/{id}/transform`
- `DELETE /api/v1/documents/{id}`

### Finance

- `GET /api/v1/finance/open-items?scope=receivables|payables`
- `GET /api/v1/finance/payments?scope=receivables|payables`
- `POST /api/v1/finance/payments`

### Stock

- `GET /api/v1/inventory/dashboard`
- `GET /api/v1/inventory/movements`
- `GET /api/v1/inventory/movements?productId={guid}&warehouseId={guid}`
- `POST /api/v1/inventory/adjustments`

## Notes de comportement

- Toutes les routes sont isolees par tenant a partir du compte connecte.
- Les transformations de documents via l'API reutilisent la logique Gescom deja en place.
- Les bons de livraison generes via l'API creent les sorties de stock.
- Les receptions generees via l'API creent les entrees de stock.
- Les suppressions sont bloquees quand une entite est deja utilisee dans le flux metier.
