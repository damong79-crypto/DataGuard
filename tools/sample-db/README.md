# 로컬 테스트용 PostgreSQL

DataGuard를 실제 DB로 시험해 보기 위한 샘플 데이터베이스입니다.
정합성 **이상이 일부러 섞인** 데이터로 구성되어, 등록한 체크 쿼리가 이상을 잡아내는 것을 확인할 수 있습니다.

## 1. 설치 (Windows)

```powershell
winget install PostgreSQL.PostgreSQL.16 --silent `
  --override "--mode unattended --unattendedmodeui none --superpassword postgres --serverport 5432 --enable-components server,commandlinetools"
```

> 슈퍼유저 `postgres` / 비밀번호 `postgres` / 포트 5432. **로컬 테스트 전용** 값입니다.

## 2. 시드 적용

```powershell
$env:PGPASSWORD = "postgres"
& "C:\Program Files\PostgreSQL\16\bin\psql.exe" -h localhost -U postgres -f tools/sample-db/postgres_seed.sql
```

생성되는 것:
- DB `dataguard_test`, 테이블 `customers` / `orders` / `payments`
- 읽기 전용 로그인 **`dataguard_ro`** / 비밀번호 `dataguard_ro`

## 3. DataGuard에 등록할 연결 정보

| 항목 | 값 |
|------|-----|
| DB 종류 | PostgreSQL |
| 호스트 / 포트 | localhost / 5432 |
| 데이터베이스 | dataguard_test |
| 사용자 / 비밀번호 | `dataguard_ro` / `dataguard_ro` (읽기 전용 권장) |

## 4. 등록할 체크 쿼리 (틀어진 행만 반환 → 0건=정상)

| 쿼리 | 기대 결과 |
|------|----------|
| 주문-결제 금액 불일치 | **이상** (1건) |
| 결제 누락 주문 | **이상** (1건) |
| 고아 주문(없는 고객) | **이상** (1건) |
| 중복 이메일 | 정상 (0건) |
| 음수 결제금액 | 정상 (0건) |

```sql
-- 1. 주문-결제 금액 불일치
SELECT o.id FROM orders o JOIN payments p ON p.order_id = o.id WHERE o.amount <> p.amount;

-- 2. 결제 누락 주문
SELECT o.id FROM orders o LEFT JOIN payments p ON p.order_id = o.id WHERE p.id IS NULL;

-- 3. 고아 주문(존재하지 않는 고객)
SELECT o.id FROM orders o LEFT JOIN customers c ON c.id = o.customer_id WHERE c.id IS NULL;

-- 4. 중복 이메일 (정상)
SELECT email FROM customers GROUP BY email HAVING COUNT(*) > 1;

-- 5. 음수 결제금액 (정상)
SELECT id FROM payments WHERE amount < 0;
```

## 5. 테스트 시나리오

1. 위 연결을 등록하고 **연결 테스트**로 접속 확인
2. 체크 쿼리 1~5를 등록 (1~3은 "이상", 4~5는 "정상" 기대)
3. **지금 실행**으로 결과·이력 확인
4. 데이터를 바꿔 가며(예: `UPDATE payments SET amount=20000 WHERE order_id=102;`) 정상 전환 확인
5. 스케줄(매 1분)·이메일 발송까지 확인 (로컬 메일 테스트는 [local-smtp](../local-smtp/README.md) 참고)

## 초기화

다시 깨끗한 상태로 되돌리려면 시드 스크립트를 다시 실행하면 됩니다(기존 DB를 DROP 후 재생성).
