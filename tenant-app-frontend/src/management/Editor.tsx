import { useState } from "react";
import { Modal, ScrollView, View, Switch, Pressable } from "react-native";
import { useMutation } from "@tanstack/react-query";
import { management, str, type Row } from "./api";
import { initialValues, payload, type Schema, type Choice } from "./forms";
import { AppText, Button, Card } from "@/ui/primitives";
import { TextField, SelectField } from "@/ui/form";
import { pickDocuments } from "@/lib/files";
import type { LocalFile } from "@/api/types";
import { colors, space } from "@/theme";
export type Catalog = Record<string, Row[]>;
export type EditorConfig = {
  schema: Schema;
  row?: Row;
  method?: string;
  extra?: Record<string, unknown>;
  onDone?: () => void;
};
function Multi({
  label,
  options,
  value,
  onChange,
}: {
  label: string;
  options: Choice[];
  value: string[];
  onChange: (v: string[]) => void;
}) {
  const [search, setSearch] = useState("");
  return (
    <View style={{ gap: 8 }}>
      <TextField
        label={label}
        value={search}
        onChangeText={setSearch}
        placeholder="Поиск получателей"
      />
      <ScrollView style={{ maxHeight: 180 }}>
        {options
          .filter((o) => o.label.toLowerCase().includes(search.toLowerCase()))
          .map((o) => (
            <Pressable
              key={o.value}
              accessibilityRole="checkbox"
              accessibilityState={{ checked: value.includes(o.value) }}
              onPress={() =>
                onChange(
                  value.includes(o.value)
                    ? value.filter((id) => id !== o.value)
                    : [...value, o.value],
                )
              }
              style={{
                padding: 10,
                backgroundColor: value.includes(o.value)
                  ? colors.accentSoft
                  : colors.card,
              }}
            >
              <AppText>
                {value.includes(o.value) ? "✓ " : ""}
                {o.label}
              </AppText>
            </Pressable>
          ))}
      </ScrollView>
      <AppText variant="caption">Выбрано: {value.length}</AppText>
    </View>
  );
}
export function Editor({
  config,
  catalog,
  onClose,
  onSaved,
}: {
  config: EditorConfig;
  catalog: Catalog;
  onClose: () => void;
  onSaved: () => void;
}) {
  const { schema } = config;
  const [values, setValues] = useState(() => initialValues(schema, config.row));
  const [files, setFiles] = useState<Record<string, LocalFile>>({});
  const [error, setError] = useState("");
  const change = (key: string, value: unknown) =>
    setValues((v) => ({
      ...v,
      [key]: value,
      ...(key === "tenantId"
        ? { leaseId: "", premisesId: "", fileId: "" }
        : {}),
    }));
  const mutation = useMutation({
    mutationFn: async () => {
      const v = { ...values };
      for (const f of schema.fields.filter((f) => f.type === "file"))
        if (files[f.key]) v[f.key] = "upload-pending";
      const data = payload(schema, v);
      for (const [key, file] of Object.entries(files)) {
        if (!data.tenantId) throw new Error("Сначала выберите арендатора.");
        if (
          schema.path === "/admin/invoices" &&
          !file.name.toLowerCase().endsWith(".pdf")
        )
          throw new Error("К счёту можно приложить только PDF.");
        const uploaded = await management.upload(file, String(data.tenantId));
        if (uploaded.state !== "Ready")
          throw new Error(
            "Файл загружен в карантин. Дождитесь проверки файла; запись ещё не создана.",
          );
        data[key] = uploaded.id;
      }
      return management.save(
        schema.path,
        { ...data, ...config.extra },
        config.method,
      );
    },
    onSuccess: () => {
      onSaved();
      config.onDone?.();
      onClose();
    },
    onError: (e: Error) => setError(e.message),
  });
  const options = (key: string): Choice[] => {
    let rows = catalog[key] ?? [];
    if (key === "staff")
      rows = (catalog.users ?? []).filter(
        (r) =>
          ["Staff", "Admin"].includes(str(r, "role")) &&
          !r.blocked &&
          r.approved,
      );
    if (key === "leases")
      rows = rows.filter((r) => r.tenantId === values.tenantId);
    if (key === "premises" && values.tenantId) {
      const ids = (catalog.leases ?? [])
        .filter((l) => l.tenantId === values.tenantId)
        .map((l) => l.premisesId);
      if (schema.path !== "/admin/leases")
        rows = rows.filter((r) => ids.includes(r.id));
    }
    return rows.map((r) => ({
      value: r.id,
      label:
        str(r, "name") ||
        str(r, "fullName") ||
        str(r, "number") ||
        str(r, "userName") ||
        r.id,
      hint: str(r, "inn") || str(r, "userName"),
    }));
  };
  return (
    <Modal
      visible
      transparent
      animationType="fade"
      onRequestClose={() => {
        if (!mutation.isPending) onClose();
      }}
    >
      <View
        style={{
          flex: 1,
          backgroundColor: "#102B3BCC",
          justifyContent: "center",
          alignItems: "center",
          padding: 12,
        }}
      >
        <Card
          style={{ width: "100%", maxWidth: 720, maxHeight: "94%", padding: 0 }}
        >
          <View
            style={{
              padding: 20,
              borderBottomWidth: 1,
              borderColor: colors.border,
              flexDirection: "row",
              alignItems: "center",
              gap: 12,
            }}
          >
            <AppText variant="h2" style={{ flex: 1 }}>
              {schema.title}
            </AppText>
            <Button
              title="Закрыть форму"
              small
              variant="ghost"
              disabled={mutation.isPending}
              onPress={onClose}
            />
          </View>
          <ScrollView
            keyboardShouldPersistTaps="handled"
            contentContainerStyle={{ padding: 20, gap: 16 }}
          >
            {schema.fields.map((f) => {
              const value = values[f.key];
              const choices = f.options ?? options(f.catalog ?? "");
              if (f.type === "bool")
                return (
                  <View
                    key={f.key}
                    style={{
                      flexDirection: "row",
                      alignItems: "center",
                      justifyContent: "space-between",
                      gap: 12,
                    }}
                  >
                    <AppText style={{ flex: 1 }}>{f.label}</AppText>
                    <Switch
                      accessibilityLabel={f.label}
                      value={!!value}
                      onValueChange={(v) => change(f.key, v)}
                    />
                  </View>
                );
              if (f.type === "multi")
                return (
                  <Multi
                    key={f.key}
                    label={f.label}
                    options={choices}
                    value={(value ?? []) as string[]}
                    onChange={(v) => change(f.key, v)}
                  />
                );
              if (
                f.key === "premisesId" &&
                schema.path.startsWith("/admin/listings/") &&
                config.row
              )
                return (
                  <View key={f.key}>
                    <AppText variant="small">Помещение объявления</AppText>
                    <AppText>
                      {choices.find((c) => c.value === value)?.label ??
                        String(value)}
                    </AppText>
                    <AppText variant="caption">
                      Для другого помещения создайте новое объявление.
                    </AppText>
                  </View>
                );
              if (f.type === "select")
                return (
                  <View key={f.key}>
                    <SelectField
                      label={f.label}
                      options={[
                        ...(!f.required
                          ? [{ value: "", label: "Не выбрано" }]
                          : []),
                        ...choices,
                      ]}
                      value={String(value ?? "")}
                      onChange={(v) => change(f.key, v)}
                    />
                    {f.hint ? (
                      <AppText variant="caption">{f.hint}</AppText>
                    ) : null}
                  </View>
                );
              if (f.type === "file")
                return (
                  <View key={f.key} style={{ gap: 8 }}>
                    <Button
                      variant="secondary"
                      title={f.label}
                      onPress={() => {
                        void pickDocuments(false)
                          .then(([file]) => {
                            if (file)
                              setFiles((v) => ({ ...v, [f.key]: file }));
                          })
                          .catch((e: Error) => setError(e.message));
                      }}
                    />
                    <AppText variant="small">
                      {files[f.key]?.name ?? "Файл не выбран. До 20 МиБ."}
                    </AppText>
                  </View>
                );
              return (
                <View key={f.key} style={{ gap: 6 }}>
                  <TextField
                    label={f.label + (f.required ? " *" : "")}
                    value={String(value ?? "")}
                    onChangeText={(v) => change(f.key, v)}
                    multiline={f.type === "long"}
                    secureTextEntry={f.type === "password"}
                    autoCapitalize="none"
                    keyboardType={
                      f.type === "number" ? "decimal-pad" : "default"
                    }
                    hint={
                      f.hint ??
                      (f.type === "date"
                        ? "ГГГГ-ММ-ДД"
                        : f.type === "datetime"
                          ? "ГГГГ-ММ-ДДTЧЧ:ММ, местное время"
                          : "")
                    }
                  />
                  {f.type === "datetime" && f.key === "publishAt" ? (
                    <Button
                      title="Сейчас"
                      small
                      variant="secondary"
                      onPress={() => change(f.key, new Date().toISOString())}
                    />
                  ) : null}
                </View>
              );
            })}
            {error ? <AppText color={colors.danger}>{error}</AppText> : null}
            <AppText variant="caption">
              * Обязательные поля. Изменения сохраняются только после нажатия
              кнопки.
            </AppText>
            <Button
              title="Сохранить"
              loading={mutation.isPending}
              onPress={() => {
                setError("");
                mutation.mutate();
              }}
            />
          </ScrollView>
        </Card>
      </View>
    </Modal>
  );
}
