# Race Management UI Workspace

This directory is a single Angular workspace containing both UI applications and shared code.

## Projects

- `race-management-cloud` - cloud-hosted UI
- `race-management-local` - on-prem/local UI
- `shared-ui` - shared reusable standalone components

## Run from one workspace

From the `ui` folder:

- `npm install`
- `npm run start:cloud`
- `npm run start:local`
- `npm run build:all`

## Shared components

Shared components live in `shared-ui/src/lib` and are exported in `shared-ui/src/public-api.ts`.

Import shared components in either app using a relative import from the app folder:

```ts
import { SharedBannerComponent } from '../../../shared-ui/src/lib/shared-banner.component';
```

Then add the component to your standalone component imports.
