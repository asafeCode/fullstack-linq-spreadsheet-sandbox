import { tv } from "tailwind-variants";
import { Badge } from "../../../shared/components/badge";
import { Card, CardContent, CardHeader, CardTitle } from "../../../shared/components/card";

const uploadProgressVariants = tv({
  slots: {
    message: "mb-2 text-sm text-muted-foreground",
    rail: "h-2 w-full rounded bg-muted",
    bar: "h-2 rounded bg-primary transition-all duration-300"
  }
});

export interface UploadProgressCardProps {
  progress: number;
  stage?: string;
  message?: string;
}

export function UploadProgressCard({ progress, stage, message }: UploadProgressCardProps) {
  const styles = uploadProgressVariants();

  return (
    <Card data-slot="upload-progress-card">
      <CardHeader>
        <CardTitle>Processing upload</CardTitle>
        <Badge variant="info">{progress}%</Badge>
      </CardHeader>
      <CardContent>
        <p className={styles.message()}>{stage ?? "Queued"} {message ? `- ${message}` : ""}</p>
        <div className={styles.rail()}>
          <div className={styles.bar()} style={{ width: `${Math.min(100, Math.max(0, progress))}%` }} />
        </div>
      </CardContent>
    </Card>
  );
}

