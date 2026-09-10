export function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div className="stat">
      <small>{label}</small>
      <b>{value}</b>
    </div>
  );
}
