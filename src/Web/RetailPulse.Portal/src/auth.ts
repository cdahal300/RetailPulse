import {
  PublicClientApplication,
} from '@azure/msal-browser'
import type { AccountInfo, AuthenticationResult, Configuration, SilentRequest } from '@azure/msal-browser'

export type PortalSession = {
  account: AccountInfo
  accessToken: string
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
      msalClient = client
      return client
    })()
  }

  return msalClient ?? await initializePromise
}

export async function signIn(): Promise<PortalSession> {
  try {
    const client = await getMsalClient()
    const result = await client.loginPopup({ scopes: [apiScope] })
    return toSession(client, result)
  } catch (error) {
    if (!isTokenRequestCacheError(error)) throw error
    clearMsalInteractionState()
    resetMsalClient()
    const client = await getMsalClient()
    const result = await client.loginPopup({ scopes: [apiScope] })
    return toSession(client, result)
  }
}

export async function signOut() {
  const client = await getMsalClient()
  await client.logoutPopup({ account: client.getActiveAccount() ?? undefined })
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
  return { account: result.account, accessToken: result.accessToken }
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
