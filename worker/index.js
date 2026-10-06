const SITE_HOST = "lib.routersys.com";
const ORIGIN_HOST = "ymm4.routersys.com";
const PREFIX = "/WorldNet";
const ROBOTS = "User-agent: *\nAllow: /\n\nSitemap: https://lib.routersys.com/WorldNet/sitemap.xml\n";

export default {
  async fetch(request) {
    if (request.method !== "GET" && request.method !== "HEAD") {
      return new Response("Method Not Allowed", { status: 405, headers: { allow: "GET, HEAD" } });
    }

    const url = new URL(request.url);

    if (url.pathname === "/robots.txt") {
      return new Response(ROBOTS, { headers: { "content-type": "text/plain; charset=utf-8" } });
    }

    if (url.pathname === "/") {
      return Response.redirect(`https://${SITE_HOST}${PREFIX}/`, 302);
    }

    if (url.pathname === PREFIX) {
      url.pathname = `${PREFIX}/`;
      return Response.redirect(url.toString(), 301);
    }

    if (!url.pathname.startsWith(`${PREFIX}/`)) {
      return new Response("Not Found", { status: 404, headers: { "content-type": "text/plain; charset=utf-8" } });
    }

    url.protocol = "https:";
    url.hostname = ORIGIN_HOST;
    url.port = "";

    const upstream = await fetch(new Request(url, request), { redirect: "manual" });
    const headers = new Headers(upstream.headers);
    const location = headers.get("location");
    if (location) {
      const target = new URL(location, url);
      if (target.hostname === ORIGIN_HOST) {
        target.protocol = "https:";
        target.hostname = SITE_HOST;
        target.port = "";
        headers.set("location", target.toString());
      }
    }

    return new Response(upstream.body, {
      status: upstream.status,
      statusText: upstream.statusText,
      headers,
    });
  },
};
