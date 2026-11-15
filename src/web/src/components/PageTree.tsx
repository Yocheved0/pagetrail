import type { PageNode } from "../api";

const percent = new Intl.NumberFormat(undefined, {
  style: "percent",
  maximumFractionDigits: 0,
});

function PageItem({ page }: { page: PageNode }) {
  return (
    <li
      role="treeitem"
      aria-expanded={page.children.length > 0 ? true : undefined}
    >
      <a href={page.url} target="_blank" rel="noreferrer">
        {page.url}
      </a>{" "}
      {page.domainLinkRatio === null ? (
        <span>{page.status}</span>
      ) : (
        <span>Domain Link Ratio {percent.format(page.domainLinkRatio)}</span>
      )}
      {page.links.length > 0 && (
        <details>
          <summary>{page.links.length} outgoing links</summary>
          <ul>
            {page.links.map((link) => (
              <li key={link}>{link}</li>
            ))}
          </ul>
        </details>
      )}
      {page.children.length > 0 && (
        <ul role="group">
          {page.children.map((child) => (
            <PageItem key={child.id} page={child} />
          ))}
        </ul>
      )}
    </li>
  );
}

export default function PageTree({ root }: { root: PageNode }) {
  return (
    <ul role="tree" aria-label="Discovered pages">
      <PageItem page={root} />
    </ul>
  );
}
