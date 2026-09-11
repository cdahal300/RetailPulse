import {
  PublicClientApplication,
} from '@azure/msal-browser'
import type { AccountInfo, AuthenticationResult, Configuration, SilentRequest } from '@azure/msal-browser'

export type PortalSession = {
  account: AccountInfo
  accessToken: string
  tenantId?: string
  storeId?: string
  roles: string[]
}

const clientId = import.meta.env.VITE_ENTRA_CLIENT_ID ?? ''
const tenantId = import.meta.env.VITE_ENTRA_TENANT_ID ?? ''
const apiScope = import.meta.env.VITE_ENTRA_API_SCOPE ?? ''
const redirectUri = import.meta.env.VITE_ENTRA_REDIRECT_URI ?? window.location.origin

export const entraConfigured = Boolean(clientId && tenantId && apiScope)

const msalConfig: Configuration = {
  auth: {
    clientId,
    authority: tenantId ? `https://login.microsoftonline.com/${tenantId}` : undefined,
    redirectUri,
  },
  cache: {
    cacheLocation: 'sessionStorage',
  },
}

let msalClient: PublicClientApplication | null = null
let initializePromise: Promise<PublicClientApplication> | null = null

async function getMsalClient() {
  if (!entraConfigured) {
    throw new Error('Entra authentication is not configured')
  }

  if (!initializePromise) {
    initializePromise = (async () => {
      const client = new PublicClientApplication(msalConfig)
      await client.initialize()
      try {
        const redirectResult = await client.handleRedirectPromise()
        if (redirectResult?.account) {
          client.setActiveAccount(redirectResult.account)
        }
      } catch (error) {
        if (!isTokenRequestCacheError(error)) throw error
        clearMsalInteractionState()
      }
      msalClient = client
      return client
    })()
  }

  return msalClient ?? await initializePromise
}

export async function signIn(): Promise<void> {
  const client = await getMsalClient()
  await client.loginRedirect({ scopes: [apiScope] })
}

export async function signOut() {
  const client = await getMsalClient()
  await client.logoutRedirect({ account: client.getActiveAccount() ?? undefined })
}

export async function getPortalSession(): Promise<PortalSession | null> {
  try {
    const client = await getMsalClient()
    const account = client.getActiveAccount() ?? client.getAllAccounts()[0]
    if (!account) return null

    client.setActiveAccount(account)
    const request: SilentRequest = { account, scopes: [apiScope] }
    const result = await client.acquireTokenSilent(request)
    return toSession(client, result)
  } catch (error) {
    if (!isTokenRequestCacheError(error)) throw error
    clearMsalInteractionState()
    resetMsalClient()
    return null
  }
}

function toSession(client: PublicClientApplication, result: AuthenticationResult): PortalSession {
  client.setActiveAccount(result.account)

  const accessTokenClaims = decodeJwtClaims(result.accessToken)
  const idTokenClaims = (result.idTokenClaims ?? result.account?.idTokenClaims ?? {}) as Record<string, unknown>
  const claims = mergeClaims(idTokenClaims, accessTokenClaims)
  const tenantId = readStringClaim(claims, 'tid') ?? readStringClaim(claims, 'tenantId') ?? undefined
  const storeId = readStringClaim(claims, 'store_id') ?? readStringClaim(claims, 'storeId') ?? undefined
  const roles = readStringArrayClaim(claims, 'roles') ?? readStringArrayClaim(claims, 'role') ?? []

  if (!tenantId || roles.length === 0) {
    throw new Error('Your Entra token is missing the required tenant or role claims for RetailPulse Manager access.')
  }

  return {
    account: result.account,
    accessToken: result.accessToken,
    tenantId,
    storeId,
    roles,
  }
}

function mergeClaims(...claimSets: Array<Record<string, unknown>>): Record<string, unknown> {
  const merged: Record<string, unknown> = {}

  for (const claims of claimSets) {
    for (const [key, value] of Object.entries(claims)) {
      if (value !== undefined && value !== null && !(key in merged)) {
        merged[key] = value
      }
    }
  }

  return merged
}

function decodeJwtClaims(token: string): Record<string, unknown> {
  const parts = token.split('.')
  if (parts.length < 2) {
    return {}
  }

  const payload = parts[1].replace(/-/g, '+').replace(/_/g, '/')
  const padded = payload.padEnd(Math.ceil(payload.length / 4) * 4, '=')

  try {
    return JSON.parse(atob(padded)) as Record<string, unknown>
  } catch {
    return {}
  }
}

function readStringClaim(claims: Record<string, unknown>, key: string): string | undefined {
  const value = claims[key]
  return typeof value === 'string' && value.trim().length > 0 ? value : undefined
}

function readStringArrayClaim(claims: Record<string, unknown>, key: string): string[] | undefined {
  const value = claims[key]
  if (Array.isArray(value)) {
    const result = value.filter((item): item is string => typeof item === 'string' && item.trim().length > 0)
    return result.length > 0 ? result : undefined
  }
  if (typeof value === 'string' && value.trim().length > 0) {
    return value.split(',').map((item) => item.trim()).filter(Boolean)
  }
  return undefined
}

function isTokenRequestCacheError(error: unknown) {
  return error instanceof Error && error.message.includes('no_token_request_cache_error')
}

function clearMsalInteractionState() {
  for (const storage of [window.sessionStorage, window.localStorage]) {
    for (let index = storage.length - 1; index >= 0; index -= 1) {
      const key = storage.key(index)
      if (key?.toLowerCase().includes('msal.interaction')) {
        storage.removeItem(key)
      }
    }
  }
}

function resetMsalClient() {
  msalClient = null
  initializePromise = null
}

