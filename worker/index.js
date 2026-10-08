const SITE_HOST = "lib.routersys.com";
const ORIGIN_HOST = "ymm4.routersys.com";
const PROJECTS = ["WorldNet", "R128Net"];
const ROBOTS = "User-agent: *\nAllow: /\n\n"
  + PROJECTS.map((name) => `Sitemap: https://${SITE_HOST}/${name}/sitemap.xml\n`).join("");

export default {
  async fetch(request) {
    if (request.method !== "GET" && request.method !== "HEAD") {
      return new Response("Method Not Allowed", { status: 405, headers: { allow: "GET, HEAD" } });
    }

    const url = new URL(request.url);

    if (url.protocol === "http:") {
      url.protocol = "https:";
      return Response.redirect(url.toString(), 301);
    }

    if (url.pathname === "/robots.txt") {
      return new Response(ROBOTS, { headers: { "content-type": "text/plain; charset=utf-8" } });
    }

    if (url.pathname === "/") {
      return Response.redirect(`https://${SITE_HOST}/${PROJECTS[0]}/`, 302);
    }

    const bare = PROJECTS.find((name) => url.pathname === `/${name}`);
    if (bare) {
      url.pathname = `/${bare}/`;
      return Response.redirect(url.toString(), 301);
    }

    if (!PROJECTS.some((name) => url.pathname.startsWith(`/${name}/`))) {
      return new Response("Not Found", { status: 404, headers: { "content-type": "text/plain; charset=utf-8" } });
    }

    url.protocol = "https:";
    url.hostname = ORIGIN_HOST;
    url.port = "";

    const upstream = await fetch(new Request(url, request), { redirect: "manual" });
    const headers = new Headers(upstream.headers);
    const directives = (headers.get("cache-control") ?? "max-age=600")
      .split(",")
      .map((directive) => directive.trim())
      .filter((directive) => directive && directive !== "public" && directive !== "no-transform");
    headers.set("cache-control", ["public", ...directives, "no-transform"].join(", "));

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
