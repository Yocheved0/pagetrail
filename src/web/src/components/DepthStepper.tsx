import { MinusIcon, PlusIcon } from "lucide-react";
import type { KeyboardEvent } from "react";
import { Button } from "./ui/button";
import { ButtonGroup } from "./ui/button-group";
import { Input } from "./ui/input";

const minDepth = 0;
const maxDepth = 5;
const singleAllowedDigit = /^[0-5]?$/;

interface DepthStepperProps {
  id: string;
  value: string;
  fallback: number;
  onChange: (value: string) => void;
}

export default function DepthStepper({
  id,
  value,
  fallback,
  onChange,
}: DepthStepperProps) {
  const depth = value === "" ? fallback : Number(value);
  const step = (delta: number) =>
    onChange(String(Math.min(maxDepth, Math.max(minDepth, depth + delta))));

  function stepWithArrows(event: KeyboardEvent) {
    if (event.key !== "ArrowUp" && event.key !== "ArrowDown") return;
    event.preventDefault();
    step(event.key === "ArrowUp" ? 1 : -1);
  }

  return (
    <ButtonGroup>
      <Button
        type="button"
        variant="outline"
        size="icon"
        aria-label="Decrease max depth"
        disabled={depth <= minDepth}
        onClick={() => step(-1)}
      >
        <MinusIcon aria-hidden />
      </Button>
      <Input
        id={id}
        role="spinbutton"
        inputMode="numeric"
        maxLength={1}
        aria-valuemin={minDepth}
        aria-valuemax={maxDepth}
        aria-valuenow={depth}
        className="w-10 text-center tabular-nums"
        placeholder={String(fallback)}
        value={value}
        onChange={(e) =>
          singleAllowedDigit.test(e.target.value) && onChange(e.target.value)
        }
        onKeyDown={stepWithArrows}
      />
      <Button
        type="button"
        variant="outline"
        size="icon"
        aria-label="Increase max depth"
        disabled={depth >= maxDepth}
        onClick={() => step(1)}
      >
        <PlusIcon aria-hidden />
      </Button>
    </ButtonGroup>
  );
}
