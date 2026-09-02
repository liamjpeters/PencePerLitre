const FUEL_FINDER_ORIGIN = "https://www.fuel-finder.service.gov.uk";

export default {
  async fetch(request, env) {
    const suppliedKey = request.headers.get("X-Proxy-Key");

    if (!suppliedKey || suppliedKey !== env.PROXY_KEY) {
      return new Response("Unauthorized", { status: 401 });
    }

    const incomingUrl = new URL(request.url);

    // Only proxy the Fuel Finder API paths.
    if (!incomingUrl.pathname.startsWith("/api/v1/")) {
      return new Response("Not found", { status: 404 });
    }

    const upstreamUrl = new URL(
      incomingUrl.pathname + incomingUrl.search,
      FUEL_FINDER_ORIGIN
    );

    const headers = new Headers(request.headers);
    headers.delete("Host");
    headers.delete("X-Proxy-Key");

    const init = {
      method: request.method,
      headers,
      redirect: "manual"
    };

    if (request.method !== "GET" && request.method !== "HEAD") {
      init.body = request.body;
    }

    const upstreamResponse = await fetch(upstreamUrl, init);

    const response = new Response(upstreamResponse.body, upstreamResponse);
    response.headers.set("Cache-Control", "no-store");
    return response;
  }
};
