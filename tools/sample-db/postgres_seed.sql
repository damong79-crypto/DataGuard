-- DataGuard 로컬 테스트용 PostgreSQL 시드 스크립트
-- 실행: psql -U postgres -f tools/sample-db/postgres_seed.sql
--
-- 정합성 "이상"을 일부러 섞어 두어, 등록한 체크 쿼리가 실제로 문제를 잡아내도록 한다.
-- 외래키를 일부러 걸지 않는다 — DB가 강제하지 않는 정합성을 DataGuard가 점검한다는 시나리오.

DROP DATABASE IF EXISTS dataguard_test;
DROP ROLE IF EXISTS dataguard_ro;

CREATE DATABASE dataguard_test;

-- DataGuard가 사용할 읽기 전용 로그인(PRD 권장: 읽기 전용 계정)
CREATE ROLE dataguard_ro LOGIN PASSWORD 'dataguard_ro';
GRANT CONNECT ON DATABASE dataguard_test TO dataguard_ro;

\c dataguard_test

CREATE TABLE customers (
    id    int PRIMARY KEY,
    name  text NOT NULL,
    email text NOT NULL
);

CREATE TABLE orders (
    id          int PRIMARY KEY,
    customer_id int NOT NULL,   -- 외래키 미설정(의도적)
    amount      numeric(12,0) NOT NULL,
    status      text NOT NULL
);

CREATE TABLE payments (
    id       int PRIMARY KEY,
    order_id int NOT NULL,       -- 외래키 미설정(의도적)
    amount   numeric(12,0) NOT NULL
);

INSERT INTO customers (id, name, email) VALUES
    (1, '김철수', 'chulsoo@example.com'),
    (2, '이영희', 'younghee@example.com'),
    (3, '박민수', 'minsoo@example.com'),
    (4, '최지은', 'jieun@example.com'),
    (5, '정해인', 'haein@example.com');

INSERT INTO orders (id, customer_id, amount, status) VALUES
    (101, 1,  10000, 'paid'),
    (102, 2,  20000, 'paid'),
    (103, 3,  15000, 'paid'),
    (104, 99,  5000, 'paid'),   -- 이상: 존재하지 않는 고객(99) + 아래에서 결제 누락
    (105, 4,  30000, 'paid');

INSERT INTO payments (id, order_id, amount) VALUES
    (1, 101, 10000),  -- 정상
    (2, 102, 18000),  -- 이상: 주문 금액(20000)과 불일치
    (3, 103, 15000),  -- 정상
    (4, 105, 30000);  -- 정상
    -- 주문 104: 결제 레코드 없음(이상)

-- 읽기 전용 권한 부여
GRANT USAGE ON SCHEMA public TO dataguard_ro;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO dataguard_ro;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON TABLES TO dataguard_ro;
