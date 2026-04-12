"""
Yahoo Fantasy Sports API - OAuth 2.0 Test Script

Performs the OAuth 2.0 authorization flow and makes a test API call
to the Yahoo Fantasy Sports API.
"""

import http.server
import json
import os
import threading
import urllib.parse
import webbrowser
from pathlib import Path

import requests

# --- Configuration ---
CLIENT_ID = os.environ.get(
    "YAHOO_CLIENT_ID",
    "",
)
CLIENT_SECRET = os.environ.get(
    "YAHOO_CLIENT_SECRET",
    "",
)
REDIRECT_URI = "https://localhost:3000"

AUTH_URL = "https://api.login.yahoo.com/oauth2/request_auth"
TOKEN_URL = "https://api.login.yahoo.com/oauth2/get_token"
API_BASE = "https://fantasysports.yahooapis.com/fantasy/v2"

TOKEN_FILE = Path(__file__).parent / ".yahoo_tokens.json"


def save_tokens(tokens: dict) -> None:
    TOKEN_FILE.write_text(json.dumps(tokens, indent=2))
    print(f"Tokens saved to {TOKEN_FILE}")


def load_tokens() -> dict | None:
    if TOKEN_FILE.exists():
        return json.loads(TOKEN_FILE.read_text())
    return None


def get_authorization_code() -> str:
    """Open browser for user authorization and capture the redirect code."""
    params = {
        "client_id": CLIENT_ID,
        "redirect_uri": REDIRECT_URI,
        "response_type": "code",
        "language": "en-us",
    }
    auth_url = f"{AUTH_URL}?{urllib.parse.urlencode(params)}"

    print("\n--- Yahoo OAuth 2.0 Authorization ---")
    print(f"Opening browser to authorize...\n")
    print(f"If the browser doesn't open, visit this URL:\n{auth_url}\n")
    webbrowser.open(auth_url)

    print(
        "After authorizing, you'll be redirected to a URL that may not load.\n"
        "Copy the FULL redirect URL from your browser's address bar and paste it here.\n"
        "(It will look like: https://localhost:3000?code=...)\n"
    )
    redirect_url = input("Paste the redirect URL here: ").strip()

    parsed = urllib.parse.urlparse(redirect_url)
    query_params = urllib.parse.parse_qs(parsed.query)

    if "code" not in query_params:
        raise ValueError(
            f"No authorization code found in the URL. Got params: {query_params}"
        )

    return query_params["code"][0]


def exchange_code_for_tokens(code: str) -> dict:
    """Exchange the authorization code for access and refresh tokens."""
    print("\nExchanging authorization code for tokens...")
    response = requests.post(
        TOKEN_URL,
        data={
            "grant_type": "authorization_code",
            "code": code,
            "redirect_uri": REDIRECT_URI,
            "client_id": CLIENT_ID,
            "client_secret": CLIENT_SECRET,
        },
        headers={"Content-Type": "application/x-www-form-urlencoded"},
    )

    if response.status_code != 200:
        print(f"Error getting tokens: {response.status_code}")
        print(response.text)
        raise Exception("Token exchange failed")

    tokens = response.json()
    print("Successfully obtained tokens!")
    return tokens


def refresh_access_token(refresh_token: str) -> dict:
    """Use a refresh token to get a new access token."""
    print("\nRefreshing access token...")
    response = requests.post(
        TOKEN_URL,
        data={
            "grant_type": "refresh_token",
            "refresh_token": refresh_token,
            "client_id": CLIENT_ID,
            "client_secret": CLIENT_SECRET,
        },
        headers={"Content-Type": "application/x-www-form-urlencoded"},
    )

    if response.status_code != 200:
        print(f"Error refreshing token: {response.status_code}")
        print(response.text)
        raise Exception("Token refresh failed")

    tokens = response.json()
    print("Successfully refreshed tokens!")
    return tokens


def make_api_request(access_token: str, endpoint: str) -> str:
    """Make an authenticated request to the Yahoo Fantasy Sports API."""
    url = f"{API_BASE}/{endpoint}"
    print(f"\nRequesting: {url}")

    response = requests.get(
        url,
        headers={
            "Authorization": f"Bearer {access_token}",
            "Accept": "application/json",
        },
    )

    print(f"Status: {response.status_code}")

    if response.status_code == 401:
        print("Access token expired or invalid.")
        return None

    return response.text


def main():
    tokens = load_tokens()

    if tokens and "refresh_token" in tokens:
        print("Found saved tokens. Attempting to refresh...")
        try:
            tokens = refresh_access_token(tokens["refresh_token"])
            save_tokens(tokens)
        except Exception:
            print("Refresh failed. Starting new authorization flow.")
            tokens = None

    if not tokens:
        code = get_authorization_code()
        tokens = exchange_code_for_tokens(code)
        save_tokens(tokens)

    access_token = tokens["access_token"]

    # --- Test API calls ---
    print("\n" + "=" * 60)
    print("TEST 1: Get current NFL game info")
    print("=" * 60)
    result = make_api_request(access_token, "game/nfl")
    if result:
        print(result[:2000])

    print("\n" + "=" * 60)
    print("TEST 2: Get user's leagues for current NFL season")
    print("=" * 60)
    result = make_api_request(
        access_token, "users;use_login=1/games;game_keys=nfl/leagues"
    )
    if result:
        print(result[:3000])

    print("\n" + "=" * 60)
    print("TEST 3: Get user's teams")
    print("=" * 60)
    result = make_api_request(
        access_token, "users;use_login=1/games;game_keys=nfl/teams"
    )
    if result:
        print(result[:3000])


if __name__ == "__main__":
    main()
