import type { APIRoute } from 'astro';
import { absoluteUrl, guides } from '../data/site';

export const GET: APIRoute = () => {
  const routes = ['/', ...Object.values(guides).map((guide) => guide.route)];
  const entries = routes.map((route) => `  <url><loc>${absoluteUrl(route)}</loc></url>`).join('\n');
  return new Response(`<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n${entries}\n</urlset>\n`, {
    headers: { 'Content-Type': 'application/xml; charset=utf-8' },
  });
};
