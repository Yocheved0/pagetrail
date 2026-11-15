import { cn } from "cn";
import { ChevronRightIcon } from "lucide-react";
import { useState } from "react";
import type { PageNode } from "../api";
import StatusBadge from "./StatusBadge";
import TruncatedTooltip from "./TruncatedTooltip";
import { Button } from "./ui/button";
import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from "./ui/collapsible";

const percent = new Intl.NumberFormat(undefined, {
  style: "percent",
  maximumFractionDigits: 0,
});

function OutgoingLinks({ links }: { links: string[] }) {
  return (
    <ul className="my-1 ml-8 grid animate-in gap-1 rounded-md border bg-muted/40 p-3 duration-200 ease-out-strong fade-in-0 slide-in-from-top-1">
      {links.map((link) => (
        <li key={link} className="flex min-w-0">
          <TruncatedTooltip text={link}>
            <span className="min-w-0 truncate">{link}</span>
          </TruncatedTooltip>
        </li>
      ))}
    </ul>
  );
}

function PageItem({ page }: { page: PageNode }) {
  const [expanded, setExpanded] = useState(true);
  const [showLinks, setShowLinks] = useState(false);
  const hasChildren = page.children.length > 0;

  return (
    <li role="treeitem" aria-expanded={hasChildren ? expanded : undefined}>
      <Collapsible open={expanded} onOpenChange={setExpanded}>
        <div className="flex flex-wrap items-center gap-x-2 gap-y-1 rounded-md py-1 hover:bg-muted/50 sm:flex-nowrap">
          {hasChildren ? (
            <CollapsibleTrigger asChild>
              <Button
                variant="ghost"
                size="icon-xs"
                aria-label={expanded ? "Collapse" : "Expand"}
              >
                <ChevronRightIcon
                  aria-hidden
                  className={cn(
                    "transition-transform duration-200 ease-out-strong",
                    expanded && "rotate-90",
                  )}
                />
              </Button>
            </CollapsibleTrigger>
          ) : (
            <span className="size-6 shrink-0" />
          )}
          <TruncatedTooltip text={page.url}>
            <a
              href={page.url}
              target="_blank"
              rel="noreferrer"
              className="min-w-0 flex-[1_1_calc(100%-2rem)] truncate hover:underline sm:flex-1"
            >
              {page.url}
            </a>
          </TruncatedTooltip>{" "}
          <span className="ml-8 flex shrink-0 items-center gap-2 sm:ml-0">
            <span className="flex w-20 shrink-0">
              <StatusBadge status={page.status} />
            </span>{" "}
            <span className="w-40 shrink-0 text-right text-xs text-muted-foreground tabular-nums">
              {page.domainLinkRatio !== null &&
                `Domain Link Ratio ${percent.format(page.domainLinkRatio)}`}
            </span>{" "}
            <span className="flex w-32 shrink-0 justify-end">
              {page.links.length > 0 && (
                <Button
                  variant="ghost"
                  size="xs"
                  className="tabular-nums"
                  aria-expanded={showLinks}
                  onClick={() => setShowLinks(!showLinks)}
                >
                  {page.links.length} outgoing links
                </Button>
              )}
            </span>
          </span>
        </div>
        {showLinks && <OutgoingLinks links={page.links} />}
        {hasChildren && (
          <CollapsibleContent className="overflow-hidden duration-200 ease-out-strong data-[state=closed]:animate-collapsible-up data-[state=open]:animate-collapsible-down">
            <ul role="group" className="ml-3 border-l pl-3">
              {page.children.map((child) => (
                <PageItem key={child.id} page={child} />
              ))}
            </ul>
          </CollapsibleContent>
        )}
      </Collapsible>
    </li>
  );
}

export default function PageTree({ root }: { root: PageNode }) {
  return (
    <ul role="tree" aria-label="Discovered pages" className="text-sm">
      <PageItem page={root} />
    </ul>
  );
}
