PGDMP  )                     }         	   mtcg_test    17.2    17.2     �           0    0    ENCODING    ENCODING        SET client_encoding = 'UTF8';
                           false            �           0    0 
   STDSTRINGS 
   STDSTRINGS     (   SET standard_conforming_strings = 'on';
                           false            �           0    0 
   SEARCHPATH 
   SEARCHPATH     8   SELECT pg_catalog.set_config('search_path', '', false);
                           false            �           1262    33021 	   mtcg_test    DATABASE     }   CREATE DATABASE mtcg_test WITH TEMPLATE = template0 ENCODING = 'UTF8' LOCALE_PROVIDER = libc LOCALE = 'German_Germany.1252';
    DROP DATABASE mtcg_test;
                     postgres    false                        3079    33037 	   uuid-ossp 	   EXTENSION     ?   CREATE EXTENSION IF NOT EXISTS "uuid-ossp" WITH SCHEMA public;
    DROP EXTENSION "uuid-ossp";
                        false            �           0    0    EXTENSION "uuid-ossp"    COMMENT     W   COMMENT ON EXTENSION "uuid-ossp" IS 'generate universally unique identifiers (UUIDs)';
                             false    2            �            1259    33048    cards    TABLE     �   CREATE TABLE public.cards (
    id uuid NOT NULL,
    name character varying(255) NOT NULL,
    damage double precision NOT NULL,
    package_id uuid,
    user_id integer,
    element character varying(50),
    type character varying(50)
);
    DROP TABLE public.cards;
       public         heap r       postgres    false            �            1259    33051    package_cards    TABLE     b   CREATE TABLE public.package_cards (
    package_id integer NOT NULL,
    card_id uuid NOT NULL
);
 !   DROP TABLE public.package_cards;
       public         heap r       postgres    false            �            1259    33054    packages    TABLE     ?   CREATE TABLE public.packages (
    package_id uuid NOT NULL
);
    DROP TABLE public.packages;
       public         heap r       postgres    false            �            1259    33057 	   user_deck    TABLE     [   CREATE TABLE public.user_deck (
    user_id integer NOT NULL,
    card_id uuid NOT NULL
);
    DROP TABLE public.user_deck;
       public         heap r       postgres    false            �            1259    33060    users    TABLE     �  CREATE TABLE public.users (
    username character varying(50) NOT NULL,
    password character varying(100) NOT NULL,
    image character varying(100),
    token character varying(100),
    coins integer DEFAULT 20,
    wins integer DEFAULT 0,
    losses integer DEFAULT 0,
    bio character varying(100),
    id integer NOT NULL,
    elo integer DEFAULT 100,
    ingame_name character varying(50)
);
    DROP TABLE public.users;
       public         heap r       postgres    false            �            1259    33069    users_id_seq    SEQUENCE     �   CREATE SEQUENCE public.users_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;
 #   DROP SEQUENCE public.users_id_seq;
       public               postgres    false    222            �           0    0    users_id_seq    SEQUENCE OWNED BY     =   ALTER SEQUENCE public.users_id_seq OWNED BY public.users.id;
          public               postgres    false    223            ?           2604    33070    users id    DEFAULT     d   ALTER TABLE ONLY public.users ALTER COLUMN id SET DEFAULT nextval('public.users_id_seq'::regclass);
 7   ALTER TABLE public.users ALTER COLUMN id DROP DEFAULT;
       public               postgres    false    223    222            �          0    33048    cards 
   TABLE DATA           U   COPY public.cards (id, name, damage, package_id, user_id, element, type) FROM stdin;
    public               postgres    false    218   �        �          0    33051    package_cards 
   TABLE DATA           <   COPY public.package_cards (package_id, card_id) FROM stdin;
    public               postgres    false    219   �        �          0    33054    packages 
   TABLE DATA           .   COPY public.packages (package_id) FROM stdin;
    public               postgres    false    220   !       �          0    33057 	   user_deck 
   TABLE DATA           5   COPY public.user_deck (user_id, card_id) FROM stdin;
    public               postgres    false    221   ,!       �          0    33060    users 
   TABLE DATA           q   COPY public.users (username, password, image, token, coins, wins, losses, bio, id, elo, ingame_name) FROM stdin;
    public               postgres    false    222   I!       �           0    0    users_id_seq    SEQUENCE SET     <   SELECT pg_catalog.setval('public.users_id_seq', 242, true);
          public               postgres    false    223            B           2606    33072    cards cards_pkey 
   CONSTRAINT     N   ALTER TABLE ONLY public.cards
    ADD CONSTRAINT cards_pkey PRIMARY KEY (id);
 :   ALTER TABLE ONLY public.cards DROP CONSTRAINT cards_pkey;
       public                 postgres    false    218            D           2606    33074     package_cards package_cards_pkey 
   CONSTRAINT     o   ALTER TABLE ONLY public.package_cards
    ADD CONSTRAINT package_cards_pkey PRIMARY KEY (package_id, card_id);
 J   ALTER TABLE ONLY public.package_cards DROP CONSTRAINT package_cards_pkey;
       public                 postgres    false    219    219            F           2606    33076    packages packages_pkey 
   CONSTRAINT     \   ALTER TABLE ONLY public.packages
    ADD CONSTRAINT packages_pkey PRIMARY KEY (package_id);
 @   ALTER TABLE ONLY public.packages DROP CONSTRAINT packages_pkey;
       public                 postgres    false    220            H           2606    33078    user_deck user_deck_pkey 
   CONSTRAINT     d   ALTER TABLE ONLY public.user_deck
    ADD CONSTRAINT user_deck_pkey PRIMARY KEY (user_id, card_id);
 B   ALTER TABLE ONLY public.user_deck DROP CONSTRAINT user_deck_pkey;
       public                 postgres    false    221    221            J           2606    33080    users users_pkey 
   CONSTRAINT     N   ALTER TABLE ONLY public.users
    ADD CONSTRAINT users_pkey PRIMARY KEY (id);
 :   ALTER TABLE ONLY public.users DROP CONSTRAINT users_pkey;
       public                 postgres    false    222            K           2606    33102    cards fk_cards_package    FK CONSTRAINT     �   ALTER TABLE ONLY public.cards
    ADD CONSTRAINT fk_cards_package FOREIGN KEY (package_id) REFERENCES public.packages(package_id) ON DELETE CASCADE;
 @   ALTER TABLE ONLY public.cards DROP CONSTRAINT fk_cards_package;
       public               postgres    false    4678    220    218            L           2606    33081 (   package_cards package_cards_card_id_fkey    FK CONSTRAINT     �   ALTER TABLE ONLY public.package_cards
    ADD CONSTRAINT package_cards_card_id_fkey FOREIGN KEY (card_id) REFERENCES public.cards(id) ON DELETE CASCADE;
 R   ALTER TABLE ONLY public.package_cards DROP CONSTRAINT package_cards_card_id_fkey;
       public               postgres    false    219    218    4674            M           2606    33086     user_deck user_deck_card_id_fkey    FK CONSTRAINT     �   ALTER TABLE ONLY public.user_deck
    ADD CONSTRAINT user_deck_card_id_fkey FOREIGN KEY (card_id) REFERENCES public.cards(id) ON DELETE CASCADE;
 J   ALTER TABLE ONLY public.user_deck DROP CONSTRAINT user_deck_card_id_fkey;
       public               postgres    false    4674    218    221            N           2606    33091     user_deck user_deck_user_id_fkey    FK CONSTRAINT     �   ALTER TABLE ONLY public.user_deck
    ADD CONSTRAINT user_deck_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.users(id) ON DELETE CASCADE;
 J   ALTER TABLE ONLY public.user_deck DROP CONSTRAINT user_deck_user_id_fkey;
       public               postgres    false    221    222    4682            �      x������ � �      �      x������ � �      �      x������ � �      �      x������ � �      �   2   x���/-N-��H,�HM)H,..�/J��!#N�8@�=... ���     