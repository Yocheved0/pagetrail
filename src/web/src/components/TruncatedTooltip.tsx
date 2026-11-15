import { useRef, useState, type ReactElement } from "react";
import { Tooltip, TooltipContent, TooltipTrigger } from "./ui/tooltip";

export default function TruncatedTooltip({
  text,
  children,
}: {
  text: string;
  children: ReactElement;
}) {
  const triggerRef = useRef<Element | null>(null);
  const [open, setOpen] = useState(false);
  const isTruncated = () => {
    const element = triggerRef.current;
    return element !== null && element.scrollWidth > element.clientWidth;
  };

  return (
    <Tooltip
      open={open}
      onOpenChange={(next) => setOpen(next && isTruncated())}
    >
      <TooltipTrigger
        asChild
        ref={(element: Element | null) => {
          triggerRef.current = element;
        }}
      >
        {children}
      </TooltipTrigger>
      <TooltipContent className="max-w-md break-all">{text}</TooltipContent>
    </Tooltip>
  );
}
